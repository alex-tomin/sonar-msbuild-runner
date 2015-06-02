﻿//-----------------------------------------------------------------------
// <copyright file="PropertiesWriter.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SonarRunner.Shim
{
    public static class PropertiesWriter
    {
        #region Public methods

        public static string ToString(ILogger logger, AnalysisConfig config, IEnumerable<ProjectInfo> projects)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (projects == null)
            {
                throw new ArgumentNullException("projects");
            }
            
            Debug.Assert(projects.Select(p => p.ProjectGuid).Distinct().Count() == projects.Count(),
                "Expecting the project guids to be unique");

            var uniqueProjects = projects.GroupBy(p => p.ProjectGuid).Where(g => g.Count() == 1).Select(g => g.First());

            StringBuilder sb = new StringBuilder();

            AppendKeyValue(sb, "sonar.projectKey", config.SonarProjectKey);
            AppendKeyValue(sb, "sonar.projectName", config.SonarProjectName);
            AppendKeyValue(sb, "sonar.projectVersion", config.SonarProjectVersion);
            AppendKeyValue(sb, "sonar.projectBaseDir", config.SonarOutputDir);
            sb.AppendLine();

            sb.AppendLine("# FIXME: Encoding is hardcoded");
            AppendKeyValue(sb, "sonar.sourceEncoding", "UTF-8");
            sb.AppendLine();

            AppendKeyValue(sb, "sonar.modules", string.Join(",", uniqueProjects.Select(p => p.GetProjectGuidAsString())));
            sb.AppendLine();

            foreach (var project in uniqueProjects)
            {
                WriteSettingsForProject(config, sb, project, logger);
            }

            return sb.ToString();
        }

        public static string Escape(string value)
        {
            StringBuilder sb = new StringBuilder();

            foreach (char c in value)
            {
                if (c == '\\')
                {
                    sb.Append("\\\\");
                }
                else if (IsAscii(c))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append("\\u");
                    sb.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Private methods

        private static void AppendKeyValue(StringBuilder sb, string keyPrefix, string keySuffix, string value)
        {
            AppendKeyValue(sb, keyPrefix + "." + keySuffix, value);
        }

        private static void AppendKeyValue(StringBuilder sb, string key, string value)
        {
            sb.Append(key);
            sb.Append('=');
            sb.AppendLine(Escape(value));
        }

        private static bool IsAscii(char c)
        {
            return c <= sbyte.MaxValue;
        }

        private static void WriteSettingsForProject(AnalysisConfig config, StringBuilder sb, ProjectInfo project, ILogger logger)
        {
            IList<string> files = project.GetFilesToAnalyze();
            Debug.Assert(files.Count > 0, "Expecting files to have a project to have files to analyze");

            string guid = project.GetProjectGuidAsString();

            AppendKeyValue(sb, guid, "sonar.projectKey", config.SonarProjectKey + ":" + guid);
            AppendKeyValue(sb, guid, "sonar.projectName", project.ProjectName);
            AppendKeyValue(sb, guid, "sonar.projectBaseDir", project.GetProjectDirectory());
            AppendKeyValue(sb, guid, "sonar.stylecop.projectFilePath", project.FullPath);

            string fxCopReport = project.TryGetAnalysisFileLocation(AnalysisType.FxCop);
            if (fxCopReport != null)
            {
                if (File.Exists(fxCopReport))
                {
                    AppendKeyValue(sb, guid, "sonar.cs.fxcop.reportPath", fxCopReport);
                }
                else
                {
                    logger.LogWarning(Resources.WARN_FxCopReportNotFound, fxCopReport);
                }
            }

            string vsCoverageReport = project.TryGetAnalysisFileLocation(AnalysisType.VisualStudioCodeCoverage);
            if (vsCoverageReport != null)
            {
                if (File.Exists(vsCoverageReport))
                {
                    AppendKeyValue(sb, guid, "sonar.cs.vscoveragexml.reportsPaths", vsCoverageReport);
                }
                else
                {
                    logger.LogWarning(Resources.WARN_CodeCoverageReportNotFound, vsCoverageReport);
                }
            }
            if (project.ProjectType == ProjectType.Product)
            {
                sb.AppendLine(guid + @".sonar.sources=\");
            }
            else
            {
                AppendKeyValue(sb, guid, "sonar.sources", "");
                sb.AppendLine(guid + @".sonar.tests=\");
            }

            for (int i = 0; i < files.Count(); i++)
            {
                var file = files[i];
                sb.Append(Escape(file));
                if (i != files.Count() - 1)
                {
                    sb.Append(@",\");
                }
                sb.AppendLine();
            }

            sb.AppendLine();
        }

        #endregion
    }
}
