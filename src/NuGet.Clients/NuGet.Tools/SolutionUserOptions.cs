// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using IStream = Microsoft.VisualStudio.OLE.Interop.IStream;

namespace NuGetVSExtension
{
    [Export]
    [Export(typeof(IUserSettingsManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class SolutionUserOptions : IUserSettingsManager, IVsPersistSolutionOpts
    {
        private const string NuGetOptionsStreamKey = "nuget";

        private readonly IServiceProvider _serviceProvider;
        private NuGetSettings _settings = new NuGetSettings();

        [ImportingConstructor]
        public SolutionUserOptions(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            IVsSolutionManager solutionManager)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            _serviceProvider = serviceProvider;

            solutionManager.SolutionOpened += (_, __) => LoadSettings();
        }

        public UserSettings GetSettings(string key)
        {
            UserSettings settings;
            if (_settings.WindowSettings.TryGetValue(key, out settings))
            {
                return settings ?? new UserSettings();
            }

            return new UserSettings();
        }

        public void AddSettings(string key, UserSettings obj)
        {
            _settings.WindowSettings[key] = obj;
        }

        public void ApplyShowDeprecatedFrameworkSetting(bool show)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var uiShell = _serviceProvider.GetService<SVsUIShell,IVsUIShell>();
            foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
            {
                var packageManagerControl = VsUtility.GetPackageManagerControl(windowFrame);
                packageManagerControl?.ApplyShowDeprecatedFrameworkSetting(show);
            }
        }

        public void ApplyShowPreviewSetting(bool show)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var uiShell = _serviceProvider.GetService<SVsUIShell, IVsUIShell>();
            foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
            {
                var packageManagerControl = VsUtility.GetPackageManagerControl(windowFrame);
                packageManagerControl?.ApplyShowPreviewSetting(show);
            }
        }

        public bool LoadSettings()
        {
            var solutionPersistence = Package.GetGlobalService(typeof(SVsSolutionPersistence)) as IVsSolutionPersistence;
            if (solutionPersistence.LoadPackageUserOpts(this, NuGetOptionsStreamKey) != VSConstants.S_OK)
            {
                return false;
            }

            return true;
        }

        public bool PersistSettings()
        {
            var solutionPersistence = Package.GetGlobalService(typeof(SVsSolutionPersistence)) as IVsSolutionPersistence;
            if (solutionPersistence.SavePackageUserOpts(this, NuGetOptionsStreamKey) != VSConstants.S_OK)
            {
                return false;
            }

            return true;
        }

        #region IVsPersistSolutionOpts

        public int LoadUserOptions(IVsSolutionPersistence pPersistence, uint grfLoadOpts)
        {
            return pPersistence.LoadPackageUserOpts(this, NuGetOptionsStreamKey);
        }

        public int ReadUserOptions(IStream pOptionsStream, string pszKey)
        {
            _settings = new NuGetSettings();

            try
            {
                using (var stream = new DataStreamFromComStream(pOptionsStream))
                {
                    var serializer = new BinaryFormatter();
                    var obj = serializer.Deserialize(stream) as NuGetSettings;
                    if (obj != null)
                    {
                        _settings = obj;
                    }
                }
            }
            catch
            {
            }

            return VSConstants.S_OK;
        }

        public int SaveUserOptions(IVsSolutionPersistence pPersistence)
        {
            pPersistence.SavePackageUserOpts(this, NuGetOptionsStreamKey);
            return VSConstants.S_OK;
        }

        public int WriteUserOptions(IStream pOptionsStream, string pszKey)
        {
            try
            {
                using (var stream = new DataStreamFromComStream(pOptionsStream))
                {
                    var serializer = new BinaryFormatter();
                    serializer.Serialize(stream, _settings);
                }
            }
            catch
            {
            }

            return VSConstants.S_OK;
        }

        #endregion IVsPersistSolutionOpts
    }
}
