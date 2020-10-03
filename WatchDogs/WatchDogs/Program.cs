using Mono.Options;
using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Configuration.Install;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;

namespace WatchDogs
{
    static class Program
    {
        static void Main(string[] args)
        {

            if (Environment.UserInteractive)
            {
                bool install = false;
                bool uninstall = false;

                var p = new OptionSet() {
                    { "i|install", "Install windows service", v => { install = true; } },
                    { "u|uninstall", "Uninstall windows service", v => { uninstall = true; } },
                };

                p.Parse(args);

                if (install) { Install(); return; }
                if (uninstall) { Uninstall(); return; }

                return;
            }
            else
            {
                try
                {
                    ServiceBase.Run(new WatchDogs());
                }
                catch (Exception ex)
                {
                    EventLog.WriteEntry(AssemblyInfo.Title, string.Format("Error starting services {0}.", ex.Message));
                }
            }
        }

        private static bool ServiceExistsGetList(string serviceName, string machineName)
        {
            ServiceController[] services = null;
            try
            {
                services = ServiceController.GetServices(machineName);
                ServiceController service = services.FirstOrDefault(s => s.ServiceName == serviceName);
                return service != null;
            }
            finally
            {
                if (services != null)
                {
                    foreach (var controller in services)
                    {
                        controller.Dispose();
                    }
                }
            }
        }

        private static void Install()
        {
            if (ServiceExistsGetList(AssemblyInfo.Title, "."))
            {
                var sc = new ServiceController(AssemblyInfo.Title);
                if (sc.CanStop && ((sc.Status.Equals(ServiceControllerStatus.Running)) ||
                                    (sc.Status.Equals(ServiceControllerStatus.Paused))))
                {
                    try
                    {
                        sc.Stop();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                ManagedInstallerClass.InstallHelper(new[]
                {"/u", Assembly.GetExecutingAssembly().Location});
            }
            ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
        }

        private static void Uninstall()
        {
            if (ServiceExistsGetList(AssemblyInfo.Title, "."))
            {
                var sc = new ServiceController(AssemblyInfo.Title);
                if (sc.CanStop && ((sc.Status.Equals(ServiceControllerStatus.Running)) ||
                                    (sc.Status.Equals(ServiceControllerStatus.Paused))))
                {
                    try
                    {
                        sc.Stop();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                ManagedInstallerClass.InstallHelper(new[] { "/u", Assembly.GetExecutingAssembly().Location });
            }
        }
    }

    [RunInstaller(true)]
    public class WindowsServiceInstaller : Installer
    {
        public WindowsServiceInstaller()
        {
            var processInstaller = new ServiceProcessInstaller();
            var serviceInstaller = new ServiceInstaller();

            AfterInstall += new InstallEventHandler(ServiceInstaller_AfterInstall);
            AfterUninstall += new InstallEventHandler(ServiceInstaller_AfterUnInstall);
            AfterRollback += new InstallEventHandler(ServiceInstaller_AfterRollback);

            processInstaller.Account = ServiceAccount.LocalSystem;
            serviceInstaller.ServiceName = AssemblyInfo.Title;
            serviceInstaller.DisplayName = AssemblyInfo.Title + " (" + AssemblyInfo.Version + ")";
            serviceInstaller.Description = AssemblyInfo.Description;

            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }

        private void ServiceInstaller_AfterInstall(object sender, InstallEventArgs e)
        {
            try
            {
                if (!EventLog.SourceExists(AssemblyInfo.Title)) { EventLog.CreateEventSource(AssemblyInfo.Title, @"Application"); }
            }
            catch (System.Security.SecurityException ex)
            {
                Debug.WriteLine(ex.Message);
                throw ex;
            }

            var ctl = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == AssemblyInfo.Title);

            Console.WriteLine("Service '" + AssemblyInfo.Title + "' have been installed.");
        }

        private void ServiceInstaller_AfterUnInstall(object sender, InstallEventArgs e)
        {
            Console.WriteLine(AssemblyInfo.Title, "Service '" + AssemblyInfo.Title + "' have been uninstalled.");
            try
            {
                if (!EventLog.SourceExists(AssemblyInfo.Title)) { EventLog.CreateEventSource(AssemblyInfo.Title, @"Application"); }
            }
            catch (System.Security.SecurityException ex)
            {
                Debug.WriteLine(ex.Message);
                throw ex;
            }
            var ctl = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == AssemblyInfo.Title);
            if (!((ctl.Status.Equals(ServiceControllerStatus.Running)) ||
                 (ctl.Status.Equals(ServiceControllerStatus.Paused))))
            {
                ctl.Start();
            }
            //Console.WriteLine(AssemblyInfo.Title, "Service '" + AssemblyInfo.Title + "' have been uninstalled.");
        }

        private void ServiceInstaller_AfterRollback(object sender, InstallEventArgs e)
        {
            EventLog.WriteEntry(AssemblyInfo.Title, "Service '" + AssemblyInfo.Title + "' - operation useccessfull: roll back performed.");
        }

    }

    public static class AssemblyInfo
    {
        private static Assembly m_assembly;

        static AssemblyInfo()
        {
            m_assembly = Assembly.GetEntryAssembly();
        }

        public static void Configure(Assembly ass)
        {
            m_assembly = ass;
        }

        public static T GetCustomAttribute<T>() where T : Attribute
        {
            object[] customAttributes = m_assembly.GetCustomAttributes(typeof(T), false);
            if (customAttributes.Length != 0)
            {
                return (T)(customAttributes[0]);
            }
            return default(T);
        }

        public static string GetCustomAttribute<T>(Func<T, string> getProperty) where T : Attribute
        {
            T customAttribute = GetCustomAttribute<T>();
            if (customAttribute != null)
            {
                return getProperty(customAttribute);
            }
            return null;
        }

        public static int GetCustomAttribute<T>(Func<T, int> getProperty) where T : Attribute
        {
            T customAttribute = GetCustomAttribute<T>();
            if (customAttribute != null)
            {
                return getProperty(customAttribute);
            }
            return 0;
        }

        public static Version Version
        {
            get
            {
                return m_assembly.GetName().Version;
            }
        }

        public static string Title
        {
            get
            {
                return GetCustomAttribute<AssemblyTitleAttribute>(
                    delegate (AssemblyTitleAttribute a) {
                        return a.Title;
                    }
                );
            }
        }

        public static string Description
        {
            get
            {
                return GetCustomAttribute<AssemblyDescriptionAttribute>(
                    delegate (AssemblyDescriptionAttribute a) {
                        return a.Description;
                    }
                );
            }
        }

        public static string Product
        {
            get
            {
                return GetCustomAttribute<AssemblyProductAttribute>(
                    delegate (AssemblyProductAttribute a) {
                        return a.Product;
                    }
                );
            }
        }


        public static string Copyright
        {
            get
            {
                return GetCustomAttribute<AssemblyCopyrightAttribute>(
                    delegate (AssemblyCopyrightAttribute a) {
                        return a.Copyright;
                    }
                );
            }
        }

        public static string Company
        {
            get
            {
                return GetCustomAttribute<AssemblyCompanyAttribute>(
                    delegate (AssemblyCompanyAttribute a) {
                        return a.Company;
                    }
                );
            }
        }

        public static string InformationalVersion
        {
            get
            {
                return GetCustomAttribute<AssemblyInformationalVersionAttribute>(
                    delegate (AssemblyInformationalVersionAttribute a) {
                        return a.InformationalVersion;
                    }
                );
            }
        }

        public static Guid AppId
        {
            get
            {
                return Guid.Parse(((GuidAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(GuidAttribute), false)).Value);
            }
        }

        public static string Location
        {
            get
            {
                return m_assembly.Location;
            }
        }

    }
}
