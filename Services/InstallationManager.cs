using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Nocturo.Common.Utilities;
using Nocturo.Downloader.Models;
using UEManifestReader.Objects;

namespace Nocturo.Downloader.Services
{
    public class InstallationManager
    {
        private readonly string _installPath;
        public InstallationManager(string installPath)
        {
            // we check if the provided path exists
            if (!Directory.Exists(installPath))
                new DirectoryNotFoundException($"Provided path '{installPath}' doesn't exist").LogErrorBeforeThrowing("InstallationManager");

            Logger.LogInfo("InstallationManager", $"Provided path: '{installPath}");
            _installPath = installPath;
        }

        public IServiceState GetService<T>(FManifest manifest, InstallationSettings settings) where T: IServiceState
        {
            if (settings == null)
                new ArgumentNullException(nameof(settings)).LogErrorBeforeThrowing("InstallationManager");

            settings.InstallPath = _installPath;
            Logger.LogInfo("InstallationManager", $"Creating instance of '{typeof(T).Name}' with settings {settings}");

            // we fetch the internal constructor
            var ctors = typeof(T).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
            if (ctors.Length <=0)
                new InvalidOperationException("Service doesn't contain matching ctor").LogErrorBeforeThrowing("InstallationManager");

            //we invoke the ctor with the following parameters
            return (IServiceState)(ctors[0]
               .Invoke(new object[2]
                {
                    manifest,
                    settings
                }));
        }

        public async Task VerifyInstallation()
        {

        }

        public async Task VerifyInstallation(FManifest manifest)
        {

        }

        public async Task DeleteInstallation()
        {

        }
    }
}