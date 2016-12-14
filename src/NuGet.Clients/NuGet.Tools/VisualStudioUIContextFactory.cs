// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;

namespace NuGetVSExtension
{
    [Export(typeof(INuGetUIContextFactory))]
    internal class VisualStudioUIContextFactory : INuGetUIContextFactory
    {
        private readonly ISourceRepositoryProvider _repositoryProvider;
        private readonly ISolutionManager _solutionManager;
        private readonly IPackageRestoreManager _restoreManager;
        private readonly IOptionsPageActivator _optionsPage;
        private readonly ISettings _settings;
        private readonly IDeleteOnRestartManager _deleteOnRestartManager;
        private readonly List<IVsPackageManagerProvider> _packageManagerProviders;
        // only pick up at most three integrated package managers
        private const int MaxPackageManager = 3;

        [ImportingConstructor]
        public VisualStudioUIContextFactory(
            ISourceRepositoryProvider repositoryProvider,
            ISolutionManager solutionManager,
            ISettings settings,
            IPackageRestoreManager packageRestoreManager,
            IOptionsPageActivator optionsPage,
            IDeleteOnRestartManager deleteOnRestartManager,
            [ImportMany]
            IEnumerable<Lazy<IVsPackageManagerProvider, IOrderable>> packageManagerProviders)
        {
            _repositoryProvider = repositoryProvider;
            _solutionManager = solutionManager;
            _restoreManager = packageRestoreManager;
            _optionsPage = optionsPage;
            _settings = settings;
            _deleteOnRestartManager = deleteOnRestartManager;
            _packageManagerProviders = PackageManagerProviderUtility.Sort(packageManagerProviders, MaxPackageManager);
        }

        public INuGetUIContext Create(NuGetPackage package, IEnumerable<NuGetProject> projects)
        {
            if (projects == null)
            {
                throw new ArgumentNullException(nameof(projects));
            }

            var packageManager = new NuGetPackageManager(
                _repositoryProvider,
                _settings,
                _solutionManager,
                _deleteOnRestartManager);

            var actionEngine = new UIActionEngine(_repositoryProvider, packageManager);

            var context = new NuGetUIContext(
                package,
                _repositoryProvider,
                _solutionManager,
                packageManager,
                actionEngine,
                _restoreManager,
                _optionsPage,
                projects,
                _packageManagerProviders);

            return context;
        }
    }
}
