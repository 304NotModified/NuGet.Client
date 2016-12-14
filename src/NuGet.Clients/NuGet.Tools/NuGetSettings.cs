// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.PackageManagement.UI;

namespace NuGetVSExtension
{
    /// <summary>
    /// The user settings that are persisted in suo file
    /// </summary>
    [Serializable]
    internal sealed class NuGetSettings
    {
        public Dictionary<string, UserSettings> WindowSettings { get; }

        public NuGetSettings()
        {
            WindowSettings = new Dictionary<string, UserSettings>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
