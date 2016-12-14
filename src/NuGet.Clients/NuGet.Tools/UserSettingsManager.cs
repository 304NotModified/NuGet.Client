// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;

namespace NuGetVSExtension
{
    [Export(typeof(IUserSettingsManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class UserSettingsManager : IUserSettingsManager
    {
        private NuGetPackage _package;

        private NuGetSettings _nugetSettings = new NuGetSettings();

        public UserSettings GetSettings(string key)
        {
            UserSettings settings;
            if (_nugetSettings.WindowSettings.TryGetValue(key, out settings))
            {
                return settings ?? new UserSettings();
            }

            return new UserSettings();
        }

        public void AddSettings(string key, UserSettings obj)
        {
            _nugetSettings.WindowSettings[key] = obj;
        }

        public void ApplyShowDeprecatedFrameworkSetting(bool show)
        {
            var serviceProvider = ServiceLocator.GetInstance<IServiceProvider>();
            IVsUIShell uiShell = (IVsUIShell)serviceProvider.GetService(typeof(SVsUIShell));
            foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
            {
                var packageManagerControl = VsUtility.GetPackageManagerControl(windowFrame);
                if (packageManagerControl != null)
                {
                    packageManagerControl.ApplyShowDeprecatedFrameworkSetting(show);
                }
            }
        }

        public void ApplyShowPreviewSetting(bool show)
        {
            var serviceProvider = ServiceLocator.GetInstance<IServiceProvider>();
            IVsUIShell uiShell = (IVsUIShell)serviceProvider.GetService(typeof(SVsUIShell));
            foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
            {
                var packageManagerControl = VsUtility.GetPackageManagerControl(windowFrame);
                if (packageManagerControl != null)
                {
                    packageManagerControl.ApplyShowPreviewSetting(show);
                }
            }
        }

        public void LoadNuGetSettings()
        {
            IVsSolutionPersistence solutionPersistence = Package.GetGlobalService(typeof(SVsSolutionPersistence)) as IVsSolutionPersistence;
            solutionPersistence.LoadPackageUserOpts(this, "nuget");
        }

        public void PersistSettings()
        {
            IVsSolutionPersistence solutionPersistence = GetGlobalService(typeof(SVsSolutionPersistence)) as IVsSolutionPersistence;
            solutionPersistence.SavePackageUserOpts(this, "nuget");
        }
    }
}
