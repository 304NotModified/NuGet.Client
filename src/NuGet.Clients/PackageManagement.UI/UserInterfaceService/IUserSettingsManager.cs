// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.UI
{
    public interface IUserSettingsManager
    {
        UserSettings GetSettings(string key);

        void AddSettings(string key, UserSettings obj);

        void LoadSettings();

        void PersistSettings();

        /// <summary>
        /// Apply the setting of whether to show preview window to all existing
        /// package manager windows after user changes it by checking/unchecking the
        /// checkbox on the preview window.
        /// </summary>
        /// <param name="show">The value of the setting.</param>
        void ApplyShowPreviewSetting(bool show);

        /// <summary>
        /// Apply the setting of whether to show the deprecated framework window to all existing
        /// package manager windows after a user changes it by checking/unchecking the checkbox on
        /// the deprecated framework window.
        /// </summary>
        /// <param name="show">The value of the setting.</param>
        void ApplyShowDeprecatedFrameworkSetting(bool show);
    }
}
