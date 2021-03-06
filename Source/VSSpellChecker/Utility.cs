﻿//===============================================================================================================
// System  : Sandcastle Help File Builder Visual Studio Package
// File    : Utility.cs
// Author  : Eric Woodruff  (Eric@EWoodruff.us)
// Updated : 02/16/2015
// Note    : Copyright 2013-2015, Eric Woodruff, All rights reserved
// Compiler: Microsoft Visual C#
//
// This file contains a utility class with extension and utility methods.
//
// This code is published under the Microsoft Public License (Ms-PL).  A copy of the license should be
// distributed with the code and can be found at the project website: https://github.com/EWSoftware/VSSpellChecker
// This notice, the author's name, and all copyright notices must remain intact in all applications,
// documentation, and source files.
//
//    Date     Who  Comments
// ==============================================================================================================
// 05/25/2013  EFW  Created the code
//===============================================================================================================

using System;
using System.Globalization;
using System.IO
;
using EnvDTE80;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;

using VisualStudio.SpellChecker.Configuration;
using VisualStudio.SpellChecker.Editors;
using VisualStudio.SpellChecker.Properties;

namespace VisualStudio.SpellChecker
{
    /// <summary>
    /// This class contains utility and extension methods
    /// </summary>
    public static class Utility
    {
        #region Constants
        //=====================================================================

        /// <summary>
        /// This is an extra <c>PredefinedTextViewRoles</c> value that only exists in VS 2013.  It defines the
        /// Peek Definition window view role which doesn't exist in earlier versions of Visual Studio.  As such,
        /// we define it here.
        /// </summary>
        public const string EmbeddedPeekTextView = "EMBEDDED_PEEK_TEXT_VIEW";
        #endregion

        #region General utility methods
        //=====================================================================

        /// <summary>
        /// Get a service from the Sandcastle Help File Builder package
        /// </summary>
        /// <param name="throwOnError">True to throw an exception if the service cannot be obtained,
        /// false to return null.</param>
        /// <typeparam name="TInterface">The interface to obtain</typeparam>
        /// <typeparam name="TService">The service used to get the interface</typeparam>
        /// <returns>The service or null if it could not be obtained</returns>
        public static TInterface GetServiceFromPackage<TInterface, TService>(bool throwOnError)
            where TInterface : class
            where TService : class
        {
            IServiceProvider provider = VSSpellCheckerPackage.Instance;

            TInterface service = (provider == null) ? null : provider.GetService(typeof(TService)) as TInterface;

            if(service == null && throwOnError)
                throw new InvalidOperationException("Unable to obtain service of type " + typeof(TService).Name);

            return service;
        }

        /// <summary>
        /// This displays a formatted message using the <see cref="IVsUIShell"/> service
        /// </summary>
        /// <param name="icon">The icon to show in the message box</param>
        /// <param name="message">The message format string</param>
        /// <param name="parameters">An optional list of parameters for the message format string</param>
        public static void ShowMessageBox(OLEMSGICON icon, string message, params object[] parameters)
        {
            Guid clsid = Guid.Empty;
            int result;

            if(message == null)
                throw new ArgumentNullException("message");

            IVsUIShell uiShell = GetServiceFromPackage<IVsUIShell, SVsUIShell>(true);

            ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(0, ref clsid,
                Resources.PackageTitle, String.Format(CultureInfo.CurrentCulture, message, parameters),
                String.Empty, 0, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST, icon, 0,
                out result));
        }

        /// <summary>
        /// Get the filename from the given text buffer
        /// </summary>
        /// <param name="buffer">The text buffer from which to get the filename</param>
        /// <returns>The filename or null if it could not be obtained</returns>
        public static string GetFilename(this ITextBuffer buffer)
        {
            ITextDocument textDoc;

            if(buffer != null && buffer.Properties.TryGetProperty(typeof(ITextDocument), out textDoc))
                if(textDoc != null && !String.IsNullOrEmpty(textDoc.FilePath))
                    return textDoc.FilePath;

            return null;
        }

        /// <summary>
        /// Get the filename extension from the given text buffer
        /// </summary>
        /// <param name="buffer">The text buffer from which to get the filename extension</param>
        /// <returns>The filename extension or null if it could not be obtained</returns>
        public static string GetFilenameExtension(this ITextBuffer buffer)
        {
            ITextDocument textDoc;
            string extension = null;

            if(buffer != null && buffer.Properties.TryGetProperty(typeof(ITextDocument), out textDoc))
                if(textDoc != null && !String.IsNullOrEmpty(textDoc.FilePath))
                    extension = Path.GetExtension(textDoc.FilePath);

            return extension;
        }

        /// <summary>
        /// This returns the given absolute file path relative to the given base path
        /// </summary>
        /// <param name="absolutePath">The file path to convert to a relative path</param>
        /// <param name="basePath">The base path to which the absolute path is made relative</param>
        /// <returns>The file path relative to the given base path</returns>
        public static string ToRelativePath(this string absolutePath, string basePath)
        {
            bool hasBackslash = false;
            string relPath;
            int minLength, idx;

            // If not specified, use the current folder as the base path
            if(basePath == null || basePath.Trim().Length == 0)
                basePath = Directory.GetCurrentDirectory();
            else
                basePath = Path.GetFullPath(basePath);

            if(absolutePath == null)
                absolutePath = String.Empty;

            // Just in case, make sure the path is absolute
            if(!Path.IsPathRooted(absolutePath))
                if(!absolutePath.Contains("*") && !absolutePath.Contains("?"))
                    absolutePath = Path.GetFullPath(absolutePath);
                else
                    absolutePath = Path.Combine(Path.GetFullPath(Path.GetDirectoryName(absolutePath)),
                        Path.GetFileName(absolutePath));

            if(absolutePath.Length > 1 && absolutePath[absolutePath.Length - 1] == '\\')
            {
                absolutePath = absolutePath.Substring(0, absolutePath.Length - 1);
                hasBackslash = true;
            }

            // Split the paths into their component parts
            char[] separators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar,
                Path.VolumeSeparatorChar };
            string[] baseParts = basePath.Split(separators);
            string[] absParts = absolutePath.Split(separators);

            // Find the common base path
            minLength = Math.Min(baseParts.Length, absParts.Length);

            for(idx = 0; idx < minLength; idx++)
                if(String.Compare(baseParts[idx], absParts[idx], StringComparison.OrdinalIgnoreCase) != 0)
                    break;

            // Use the absolute path if there's nothing in common (i.e. they are on different drives or network
            // shares.
            if(idx == 0)
                relPath = absolutePath;
            else
            {
                // If equal to the base path, it doesn't have to go anywhere.  Otherwise, work up from the base
                // path to the common root.
                if(idx == baseParts.Length)
                    relPath = String.Empty;
                else
                    relPath = new String(' ', baseParts.Length - idx).Replace(" ", ".." +
                        Path.DirectorySeparatorChar);

                // And finally, add the path from the common root to the absolute path
                relPath += String.Join(Path.DirectorySeparatorChar.ToString(), absParts, idx,
                    absParts.Length - idx);
            }

            return (hasBackslash) ? relPath + "\\" : relPath;
        }
        #endregion

        #region Property state conversion methods
        //=====================================================================

        /// <summary>
        /// Convert the named property value to the appropriate selection state
        /// </summary>
        /// <param name="configuration">The configuration file from which to obtain the property value</param>
        /// <param name="propertyName">The name of the property to get</param>
        /// <returns>The selection state based on the specified property's value</returns>
        public static PropertyState ToPropertyState(this SpellingConfigurationFile configuration,
          string propertyName)
        {
            return !configuration.HasProperty(propertyName) &&
                configuration.ConfigurationType != ConfigurationType.Global ? PropertyState.Inherited :
                configuration.ToBoolean(propertyName) ? PropertyState.Yes : PropertyState.No;
        }

        /// <summary>
        /// Convert the selection state value to a property value
        /// </summary>
        /// <param name="state">The selection state to convert</param>
        /// <returns>The appropriate property value to store</returns>
        public static bool? ToPropertyValue(this PropertyState state)
        {
            return (state == PropertyState.Inherited) ? (bool?)null : state == PropertyState.Yes ? true : false;
        }

        #endregion

        #region Project and source control interaction
        //=====================================================================

        /// <summary>
        /// This is used to determine if the given user dictionary words file can be written to
        /// </summary>
        /// <param name="dictionaryWordsFile">The user dictionary words file</param>
        /// <param name="dictionaryFile">The related dictionary file</param>
        /// <param name="serviceProvider">The service provider to use for interacting with the solution/project</param>
        /// <returns>True if it can, false if not</returns>
        public static bool CanWriteToUserWordsFile(this string dictionaryWordsFile, string dictionaryFile,
          IServiceProvider serviceProvider)
        {
            if(String.IsNullOrWhiteSpace(dictionaryWordsFile))
                throw new ArgumentException("Dictionary words file cannot be null or empty", "dictionaryWordsFile");

            if(String.IsNullOrWhiteSpace(dictionaryFile))
                throw new ArgumentException("Dictionary file cannot be null or empty", "dictionaryFile");

            // The file must exist
            if(!File.Exists(dictionaryWordsFile))
                File.WriteAllText(dictionaryWordsFile, String.Empty);

            // If no service provider or it's in the global folder, we can write to it if not read-only
            if(serviceProvider == null || Path.GetDirectoryName(dictionaryWordsFile).StartsWith(
              SpellingConfigurationFile.GlobalConfigurationFilePath, StringComparison.OrdinalIgnoreCase))
                return ((File.GetAttributes(dictionaryWordsFile) & FileAttributes.ReadOnly) == 0);

            var dte = serviceProvider.GetService(typeof(SDTE)) as DTE2;

            // If not part of an active solution, we can write to it if not read-only
            if(dte == null || dte.Solution == null || String.IsNullOrWhiteSpace(dte.Solution.FullName))
                return ((File.GetAttributes(dictionaryWordsFile) & FileAttributes.ReadOnly) == 0);

            // See if the user file or its related dictionary is part of the solution.  If neither are, we can
            // write to it if not read-only.
            var userItem = dte.Solution.FindProjectItem(dictionaryWordsFile);
            var dictItem = dte.Solution.FindProjectItem(dictionaryFile);

            if(dictItem == null && userItem == null)
                return ((File.GetAttributes(dictionaryWordsFile) & FileAttributes.ReadOnly) == 0);

            // If the dictionary is part of a project but the user file isn't, add it to the dictionary file's
            // containing project.
            if(dictItem.ContainingProject != null && userItem == null)
            {
                userItem = dictItem.ContainingProject.ProjectItems.AddFromFile(dictionaryWordsFile);

                if(userItem == null)
                    return false;
            }

            // If under source control, check it out
            if(dte.SourceControl.IsItemUnderSCC(dictionaryWordsFile) &&
              !dte.SourceControl.IsItemCheckedOut(dictionaryWordsFile))
                return dte.SourceControl.CheckOutItem(dictionaryWordsFile);

            return ((File.GetAttributes(dictionaryWordsFile) & FileAttributes.ReadOnly) == 0);
        }
        #endregion
    }
}
