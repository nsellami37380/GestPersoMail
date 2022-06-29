using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Runtime.InteropServices;
using System.Web;
using System.Net.Mail;
using System.Data.Entity;

namespace GestPersoMail
{

   public partial class GestPersoService : ServiceBase
   {
      public const string EMAILADMIN = "nourredinesellami@hotmail.com";  //      emailTo = "graziella.gautier@univ-tours.fr";

      BD_GESTPERSOEntities db = new BD_GESTPERSOEntities();
      public GestPersoService()
      {
         InitializeComponent();
         eventLog1 = new System.Diagnostics.EventLog();
         if (!System.Diagnostics.EventLog.SourceExists("MySource"))
         {
            System.Diagnostics.EventLog.CreateEventSource(
                "MySource", "MyNewLog");
         }
         eventLog1.Source = "MySource";
         eventLog1.Log = "MyNewLog";
      }

      private string GetImportancebyId(int id)
      {
         switch(id)
         {
            case 0: return "Faible";
            case 1: return "Normale";
            case 2: return "Prioritaire";
            case 3: return "Urgente";
            default: return "Inconnue";
         }
      }
      protected override void OnStart(string[] args)
      {
         // Update the service state to Start Pending.
         ServiceStatus serviceStatus = new ServiceStatus();
         serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
         serviceStatus.dwWaitHint = 100000;
         SetServiceStatus(this.ServiceHandle, ref serviceStatus);

         eventLog1.WriteEntry("In OnStart.");
         Timer timer = new Timer();
         timer.Interval = 21600000; // 60000*60*6 = 21600000   6 heures
         timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
         timer.Start();


         // Update the service state to Running.
         serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
         SetServiceStatus(this.ServiceHandle, ref serviceStatus);
      }



      private void OnTimer(object sender, ElapsedEventArgs e)
      {
         try
         {
            //récupération des taches qui passent Urgentes

            for (int i = 3; i >=0; i--)
            {
               IQueryable<Taches> taches = GetTacthesForMail(i);
               if (taches.Count() != 0) sendMail(taches, i);      
            }
         }
         catch (Exception E)
         {
            eventLog1.WriteEntry("une erreur s'est produite: " + E.InnerException.Message);
         }         
      }

      private void sendMail (IQueryable<Taches> taches, int nouvelleImportance)
      {
         
         foreach (var tache in taches)
         {

            string objet = "changement importance";
            string message = string.Format("La tache {0} est passée de l'importance {1} à {2} ",
               tache.nom, GetImportancebyId(tache.importance), GetImportancebyId(nouvelleImportance));

            // si un utilisateur est affecté à la tache on récupère son email sinon mail à l'admin
            int? uid = tache.utilisateur;
            Utilisateurs u = null;
            if (uid != null)
               u = GetUtilisateurBYId(uid);
            string emailTo;
            if (u != null)
            {
               emailTo = u.email;
            }
            else
            {
               emailTo = EMAILADMIN;
            }

            MailMessage mail = new MailMessage("nourredinesellami@gmail.com", emailTo, objet, message);
            SmtpClient client = new SmtpClient("smtp.gmail.com");
            client.Port = 587;
            client.Credentials = new System.Net.NetworkCredential("nourredinesellami@gmail.com", "qgqqqkupgftxbcom");
            client.EnableSsl = true;
            client.Send(mail);

            eventLog1.WriteEntry(string.Format("Un email à été envoyé à {0} concernant la tache {1} qui est passé {2}",
               emailTo, tache.nom, GetImportancebyId(nouvelleImportance))) ;

            tache.importance = 3;
            db.Entry(tache).State = EntityState.Modified;
         }
         db.SaveChanges();

      }

      protected override void OnStop()
      {
         eventLog1.WriteEntry("In OnStop.");
      }

      protected override void OnContinue()
      {
         eventLog1.WriteEntry("In OnContinue.");
      }

      public enum ServiceState
      {
         SERVICE_STOPPED = 0x00000001,
         SERVICE_START_PENDING = 0x00000002,
         SERVICE_STOP_PENDING = 0x00000003,
         SERVICE_RUNNING = 0x00000004,
         SERVICE_CONTINUE_PENDING = 0x00000005,
         SERVICE_PAUSE_PENDING = 0x00000006,
         SERVICE_PAUSED = 0x00000007,
      }

      [StructLayout(LayoutKind.Sequential)]
      public struct ServiceStatus
      {
         public int dwServiceType;
         public ServiceState dwCurrentState;
         public int dwControlsAccepted;
         public int dwWin32ExitCode;
         public int dwServiceSpecificExitCode;
         public int dwCheckPoint;
         public int dwWaitHint;
      };


      [DllImport("advapi32.dll", SetLastError = true)]
      private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);

      public IQueryable<Taches> GetTacthesForMail(int importance)
      {
         IQueryable<Taches> query = null;
         switch (importance)
         {
            // On récupére les taches dont l'importance est passée Normale
            case 1:
                query = from taches in db.Taches
                           where taches.Termine == false && taches.importance < 1 && taches.date_normale <= DateTime.Now
                           select taches;
               break;
            // On récupére les taches dont l'importance est passée Prioritaire
            case 2:
               query = from taches in db.Taches
                           where taches.Termine == false && taches.importance < 2 && taches.date_prioritaire <= DateTime.Now
                           select taches;
               break;
            // On récupére les taches dont l'importance est passée urgente
            case 3:
                query = from taches in db.Taches
                           where taches.Termine == false && taches.importance < 3 && taches.date_urgente <= DateTime.Now
                           select taches;
               break;
         }
         return query;


      }

      public Utilisateurs GetUtilisateurBYId(int? ident)
      {
         using (BD_GESTPERSOEntities db2 = new BD_GESTPERSOEntities())
         {
            if (ident == null) return null;
            Utilisateurs utilisateur = db2.Utilisateurs.Find(ident);
            return utilisateur;
         }


      }


   }
}
