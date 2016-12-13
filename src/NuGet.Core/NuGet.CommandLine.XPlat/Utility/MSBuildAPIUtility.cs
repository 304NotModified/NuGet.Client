﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.CommandLine.XPlat
{
    public class MSBuildAPIUtility
    {
        private static string PACKAGE_REFERENCE_TYPE_TAG = "PackageReference";
        private static string VERSION_TAG = "Version";
        private static string FRAMEWORK_TAG = "TargetFramework";
        private static string FRAMEWORKS_TAG = "TargetFrameworks";

        /// <summary>
        /// Opens an MSBuild.Evaluation.Project type from a csproj file.
        /// </summary>
        /// <param name="projectCSProjPath">CSProj file which needs to be evaluated</param>
        /// <returns>MSBuild.Evaluation.Project</returns>
        public Project GetProject(string projectCSProjPath)
        {
            var projectRootElement = TryOpenProjectRootElement(projectCSProjPath);
            if (projectCSProjPath == null)
            {
                throw new Exception(string.Format(CultureInfo.CurrentCulture, Strings.Error_MSBuildUnableToOpenProject));
            }
            return new Project(projectRootElement);
        }

        /// <summary>
        /// Opens an MSBuild.Evaluation.Project type from a csproj file with the given global properties.
        /// </summary>
        /// <param name="projectCSProjPath">CSProj file which needs to be evaluated</param>
        /// <param name="globalProperties">Global properties that should be used to evaluate the project while opening.</param>
        /// <returns>MSBuild.Evaluation.Project</returns>
        public Project GetProject(string projectCSProjPath, IDictionary<string, string> globalProperties)
        {
            var projectRootElement = TryOpenProjectRootElement(projectCSProjPath);
            if (projectCSProjPath == null)
            {
                throw new Exception(string.Format(CultureInfo.CurrentCulture, Strings.Error_MSBuildUnableToOpenProject));
            }
            return new Project(projectRootElement, globalProperties, toolsVersion: null);
        }

        /// <summary>
        /// Add an unconditional package reference to the project.
        /// </summary>
        /// <param name="projectPath">Path to the csproj file of the project.</param>
        /// <param name="packageIdentity">Package Identity of the package to be added.</param>
        public void AddPackageReference(string projectPath, PackageIdentity packageIdentity)
        {
            var project = GetProject(projectPath);
            var existingPackageReferences = GetPackageReferencesForAllFrameworks(project, packageIdentity);
            AddPackageReference(project, packageIdentity, existingPackageReferences);
        }

        /// <summary>
        /// Add conditional package reference to the project per target framework.
        /// </summary>
        /// <param name="projectPath">Path to the csproj file of the project.</param>
        /// <param name="packageIdentity">Package Identity of the package to be added.</param>
        /// <param name="frameworks">Target Frameworks for which the package reference should be added.</param>
        public void AddPackageReferencePerTFM(string projectPath, PackageIdentity packageIdentity, IEnumerable<string> frameworks)
        {
            foreach (var framework in frameworks)
            {
                var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "TargetFramework", framework } };
                var project = GetProject(projectPath, globalProperties);
                var existingPackageReferences = GetPackageReferences(project, packageIdentity);
                AddPackageReference(project, packageIdentity, existingPackageReferences, framework);
            }
        }

        private void AddPackageReference(Project project, PackageIdentity packageIdentity, IEnumerable<ProjectItem> existingPackageReferences, string framework = null)
        {
            var itemGroups = GetItemGroups(project);

            if (existingPackageReferences.Count() == 0)
            {
                // Add packageReference only if it does not exist.
                var itemGroup = GetItemGroup(project, itemGroups, PACKAGE_REFERENCE_TYPE_TAG);
                if (framework != null)
                {
                    itemGroup.Condition = GetTargetFrameworkCondition(framework);
                }
                AddPackageReferenceIntoItemGroup(itemGroup, packageIdentity);
            }
            else
            {
                // If the package already has a reference then try to update the reference.
                UpdatePackageReferenceItems(existingPackageReferences, packageIdentity);
            }
            project.Save();
        }

        private IEnumerable<ProjectItemGroupElement> GetItemGroups(Project project)
        {
            return project
                .Items
                .Select(item => item.Xml.Parent as ProjectItemGroupElement)
                .Distinct();
        }

        private ProjectItemGroupElement GetItemGroup(Project project, IEnumerable<ProjectItemGroupElement> itemGroups, string itemType)
        {
            var itemGroup = itemGroups?
                .Where(itemGroupElement => itemGroupElement.Items.Any(item => item.ItemType == itemType))?
                .FirstOrDefault();

            // itemGroup will contain be an item group that has a package reference tag and meets the condition.
            // itemGroup could be null here.

            if (itemGroup == null)
            {
                // This means that either no item groups exist that match the condition
                // or they do not have a package reference tag

                itemGroup = project.Xml.AddItemGroup();
            }

            return itemGroup;
        }

        private void UpdatePackageReferenceItems(IEnumerable<ProjectItem> packageReferencesItems, PackageIdentity packageIdentity)
        {
            foreach (var packageReferenceItem in packageReferencesItems)
            {
                var versionMetadata = packageReferenceItem
                    .Metadata
                    .Where(m => m.Name.Equals(VERSION_TAG, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
                if (versionMetadata == null)
                {
                    packageReferenceItem.SetMetadataValue(VERSION_TAG, packageIdentity.Version.ToNormalizedString());
                }
                else
                {
                    versionMetadata.UnevaluatedValue = packageIdentity.Version.ToNormalizedString();
                }
            }
        }

        private void AddPackageReferenceIntoItemGroup(ProjectItemGroupElement itemGroup, PackageIdentity packageIdentity)
        {
            var packageMetadata = new Dictionary<string, string> { { VERSION_TAG, packageIdentity.Version.ToString() } };

            // Currently metadata is added as a metadata. As opposed to an attribute
            // Due to https://github.com/Microsoft/msbuild/issues/1393

            itemGroup.AddItem(PACKAGE_REFERENCE_TYPE_TAG, packageIdentity.Id, packageMetadata);
        }

        /// <summary>
        /// Returns all package references after evaluating the condition on the item groups.
        /// This method is used when we need package references for a specific target framework.
        /// </summary>
        /// <param name="project">Project for which the package references have to be obtained.
        /// The project should have the global property set to have a specific framework</param>
        /// <param name="packageIdentity">Identity of the package.</param>
        /// <returns>List of Items containing the package reference for the package.</returns>
        private IEnumerable<ProjectItem> GetPackageReferences(Project project, PackageIdentity packageIdentity)
        {
            return project.AllEvaluatedItems
                .Where(item => item.ItemType.Equals(PACKAGE_REFERENCE_TYPE_TAG, StringComparison.OrdinalIgnoreCase) &&
                               item.EvaluatedInclude.Equals(packageIdentity.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Returns all package references after evaluating the condition on the item groups.
        /// This method is used when we need package references for a specific target framework.
        /// </summary>
        /// <param name="project">Project for which the package references have to be obtained.
        /// The project should have the global property set to have a specific framework</param>
        /// <param name="packageIdentity">Identity of the package.</param>
        /// <returns>List of Items containing the package reference for the package.</returns>
        private IEnumerable<ProjectItem> GetPackageReferencesForAllFrameworks(Project project, PackageIdentity packageIdentity)
        {
            var frameworks = GetProjectFrameworks(project);
            var mergedPackageReferences = new List<ProjectItem>();
            foreach (var framework in frameworks)
            {
                var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "TargetFramework", framework } };
                var projectPerFramework = GetProject(project.FullPath, globalProperties);

                mergedPackageReferences.AddRange(projectPerFramework.AllEvaluatedItems
                .Where(item => item.ItemType.Equals(PACKAGE_REFERENCE_TYPE_TAG, StringComparison.OrdinalIgnoreCase) &&
                               item.EvaluatedInclude.Equals(packageIdentity.Id, StringComparison.OrdinalIgnoreCase)));
            }
            return mergedPackageReferences;
        }

        private IEnumerable<string> GetProjectFrameworks(Project project)
        {
            var frameworks = project
                .AllEvaluatedProperties
                .Where(p => p.Name.Equals(FRAMEWORK_TAG, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.EvaluatedValue);

            if (frameworks.Count() == 0)
            {
                var frameworksString = project
                    .AllEvaluatedProperties
                    .Where(p => p.Name.Equals(FRAMEWORKS_TAG, StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.EvaluatedValue)
                    .FirstOrDefault();
                frameworks = StringUtility.Split(frameworksString);
            }
            return frameworks;
        }

        private ProjectRootElement TryOpenProjectRootElement(string filename)
        {
            try
            {
                // There is ProjectRootElement.TryOpen but it does not work as expected
                // I.e. it returns null for some valid projects
                return ProjectRootElement.Open(filename);
            }
            catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
            {
                return null;
            }
        }

        private string GetTargetFrameworkCondition(string targetFramework)
        {
            return string.Format("'$(TargetFramework)' == '{0}'", targetFramework);
        }
    }
}