﻿//
// Copyright (c) Seal Report (sealreport@gmail.com), http://www.sealreport.org.
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. http://www.apache.org/licenses/LICENSE-2.0..
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Data;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using Seal.Helpers;
using Seal.Forms;
using DynamicTypeDescriptor;
using System.Xml;

namespace Seal.Model
{
    /// <summary>
    /// SealSecurity defines all the repository security
    /// </summary>
    public class SealSecurity : RootEditor
    {
        /// <summary>
        /// Current file path
        /// </summary>
        [XmlIgnore]
        public string FilePath;

        /// <summary>
        /// Current repository
        /// </summary>
        [XmlIgnore]
        public Repository Repository;

        #region Editor

        protected override void UpdateEditorAttributes()
        {
            if (_dctd != null)
            {
                //Disable all properties
                foreach (var property in Properties) property.SetIsBrowsable(false);
                //Then enable
                GetProperty("CurrentParameters").SetIsBrowsable(true);
                GetProperty("ProviderName").SetIsBrowsable(true);
                GetProperty("Groups").SetIsBrowsable(true);
                //GetProperty("ProviderScript").SetIsBrowsable(true);
                GetProperty("UseCustomScript").SetIsBrowsable(true);
                GetProperty("Script").SetIsBrowsable(true);
                GetProperty("Error").SetIsBrowsable(true);
                GetProperty("TestUserName").SetIsBrowsable(true);
                GetProperty("TestPassword").SetIsBrowsable(true);
                GetProperty("TestCurrentWindowsUser").SetIsBrowsable(true);
                GetProperty("HelperSimulateLogin").SetIsBrowsable(true);

                //GetProperty("TestUserName").SetIsReadOnly(!Provider.PromptUserPassword);
                //GetProperty("TestPassword").SetIsReadOnly(!Provider.PromptUserPassword);

                TypeDescriptor.Refresh(this);
            }
        }

        #endregion

        string _providerName;
        /// <summary>
        /// The security provider used for the authentication
        /// </summary>
        [DisplayName("Security Provider"), Description("The security provider used for the authentication. Security providers are defined in the repository Security\\Providers folder."), Category("Security Provider Definition"), Id(1, 1)]
        [TypeConverter(typeof(SecurityProviderConverter))]
        public string ProviderName
        {
            get { return _providerName; }
            set
            {
                _providerName = value;
                UpdateEditorAttributes();
            }
        }

        /// <summary>
        /// The script executed to login and find the security group used to published reports
        /// </summary>
        [Category("Security Provider Definition"), DisplayName("Provider Security Script"), Description("The script executed to login and find the security group used to published reports."), Id(3, 1)]
        [Editor(typeof(TemplateTextEditor), typeof(UITypeEditor))]
        [XmlIgnore]
        public string ProviderScript
        {
            get { return Provider.Script; }
        }

        bool _useCustomScript = false;
        /// <summary>
        /// If true, a custom script can be used for the authentication process
        /// </summary>
        [Category("Security Provider Configuration"), DisplayName("Use custom Security Script"), Description("If true, a custom script can be used for the authentication process."), Id(1, 2)]
        [DefaultValue(false)]
        public bool UseCustomScript
        {
            get { return _useCustomScript; }
            set
            {
                _useCustomScript = value;
                UpdateEditorAttributes();
            }
        }

        /// <summary>
        /// The script executed to login and find the security group used to published reports. If the script is empty, the publication is done using the first security group defined.
        /// </summary>
        [Category("Security Provider Configuration"), DisplayName("Custom Security Script"), Description("The script executed to login and find the security group used to published reports. If the script is empty, the publication is done using the first security group defined."), Id(2, 2)]
        [Editor(typeof(TemplateTextEditor), typeof(UITypeEditor))]
        public string Script { get; set; }

        /// <summary>
        /// Current SecurityProvider
        /// </summary>
        public SecurityProvider Provider
        {
            get
            {
                var result = Providers.FirstOrDefault(i => i.Name == ProviderName);
                if (result == null) result = Providers.FirstOrDefault(i => i.Name == "No Security");
                if (result == null) result = Providers.FirstOrDefault();
                if (result == null) throw new Exception("Invalid configuration: No security providers available. Check your repository configuration...");
                _providerName = result.Name;
                return result;
            }
        }


        List<SecurityProvider> _providers = null;
        /// <summary>
        /// List of SecurityProviders available in the repository
        /// </summary>
        [XmlIgnore]
        public List<SecurityProvider> Providers
        {
            get
            {
                if (_providers == null)
                {
                    _providers = SecurityProvider.LoadProviders(Repository.SecurityProvidersFolder);
                }
                return _providers;
            }
        }

        /// <summary>
        /// List of SecurityParameter used by the provider
        /// </summary>
        public List<SecurityParameter> Parameters { get; set; } = new List<SecurityParameter>();
        public bool ShouldSerializeParameters() { return Parameters.Count > 0; }

        /// <summary>
        /// Parameter values used in the script
        /// </summary>
        [Editor(typeof(EntityCollectionEditor), typeof(UITypeEditor))]
        [Category("Security Provider Configuration"), DisplayName("Parameters"), Description("Parameter values used in the script."), Id(3, 2)]
        [XmlIgnore]
        public List<SecurityParameter> CurrentParameters
        {
            get
            {
                //Copy existing parameter values
                for (int i = 0; i < Parameters.Count; i++)
                {
                    var param = Parameters[i];
                    var paramProvider = Provider.Parameters.FirstOrDefault(j => j.Name == param.Name);
                    if (paramProvider != null) paramProvider.Value = param.Value;
                }
                //Then use the parameters of the provider
                Parameters.AddRange(Provider.Parameters.Where(i => !Parameters.Exists(j => j.Name == i.Name)));
                return Provider.Parameters;
            }
            set { Parameters = value; }
        }

        /// <summary>
        /// The groups defines how are published folders and reports in the Web Report Server. At least one group must exist.
        /// </summary>
        [Category("Groups"), DisplayName("Security Groups"), Description("The groups defines how are published folders and reports in the Web Report Server. At least one group must exist."), Id(1, 1)]
        [Editor(typeof(EntityCollectionEditor), typeof(UITypeEditor))]
        public List<SecurityGroup> Groups { get; set; } = new List<SecurityGroup>();
        public bool ShouldSerializeGroups() { return Groups.Count > 0; }

        /// <summary>
        /// Returns a SecurityFolder from a given name
        /// </summary>
        public SecurityFolder FindSecurityFolder(List<SecurityGroup> groups, string folder)
        {
            SecurityFolder result = null;
            foreach (var group in groups)
            {
                string folderRoot = folder;
                SecurityFolder current = null;
                while (current == null && !string.IsNullOrEmpty(folderRoot))
                {
                    current = group.Folders.FirstOrDefault(i => i.Path == folderRoot);
                    folderRoot = Path.GetDirectoryName(folderRoot);
                }
                if (current != null && current.Path != folder && !current.UseSubFolders)
                {
                    //cannot use this parent
                    current = null;
                }

                if (current != null)
                {
                    if (result != null)
                    {
                        //Merge the groupFolder find in this group with the current result
                        //Highest right is applied..
                        result.FolderRight = (FolderRight)Math.Max((int)result.FolderRight, (int)current.FolderRight);
                        result.ExpandSubFolders = result.ExpandSubFolders || current.ExpandSubFolders;
                        result.ManageFolder = result.ManageFolder && current.ManageFolder;
                    }
                    else
                    {
                        result = (SecurityFolder)Helper.Clone(current);
                    }

                    //set IsDefined flag
                    if (current.Path == folder) result.IsDefined = true;
                }
            }
            return result;
        }


        string getSecuritySummary(SecurityGroup group, string folder)
        {
            StringBuilder result = new StringBuilder();
            List<SecurityGroup> groups = new List<SecurityGroup>();
            groups.Add(group);
            string folder2 = folder.Substring(Repository.ReportsFolder.Length);
            SecurityFolder securityFolder = FindSecurityFolder(groups, folder2);
            if (securityFolder != null)
            {
                result.AppendLine(string.Format("    Folder:'{0}' => Right:{1}; Use Sub-folders:{2}; Manage Sub-folders:{3}; Files Only:{4}\r\n", folder2, Helper.GetEnumDescription(securityFolder.FolderRight.GetType(), securityFolder.FolderRight), securityFolder.UseSubFolders ? "Yes" : "No", securityFolder.UseSubFolders && securityFolder.ManageFolder ? "Yes" : "No", securityFolder.FilesOnly ? "Yes" : "No"));
            }

            foreach (string subFolder in Directory.GetDirectories(folder))
            {
                result.Append(getSecuritySummary(group, subFolder));
            }
            return result.ToString();
        }

        /// <summary>
        /// Security summary
        /// </summary>
        public string GetSecuritySummary()
        {
            StringBuilder result = new StringBuilder();

            initSecurity();
            foreach (var group in Groups)
            {
                result.AppendLine(string.Format("Security Group: {0}\r\n", group.Name));
                result.AppendFormat("    Personal Folder: {0}\r\n", Helper.GetEnumDescription(group.PersFolderRight.GetType(), group.PersFolderRight));
                result.AppendLine();
                result.AppendFormat("    Show all folders: {0}\r\n", group.ShowAllFolders ? "yes" : "no");
                result.AppendLine();
                result.AppendFormat("    SQL Models: {0}\r\n", group.SqlModel ? "yes" : "no");
                result.AppendLine();
                foreach (var item in group.Devices)
                {
                    result.AppendFormat("    Device:'{0}'  => Right:{1}\r\n", item.DisplayName, Helper.GetEnumDescription(item.Right.GetType(), item.Right));
                }
                foreach (var item in group.Sources)
                {
                    result.AppendFormat("    Source:'{0}'  => Right:{1}\r\n", item.DisplayName, Helper.GetEnumDescription(item.Right.GetType(), item.Right));
                }
                foreach (var item in group.Connections)
                {
                    result.AppendFormat("    Connection:'{0}'  => Right:{1}\r\n", item.DisplayName, Helper.GetEnumDescription(item.Right.GetType(), item.Right));
                }
                foreach (var item in group.Columns)
                {
                    result.AppendFormat("    Column:'{0}'  => Right:{1}\r\n", item.DisplayName, Helper.GetEnumDescription(item.Right.GetType(), item.Right));
                }
                result.AppendLine();

                result.AppendLine(getSecuritySummary(group, Repository.ReportsFolder + Path.DirectorySeparatorChar.ToString()));
            }
            return result.ToString();
        }

        void initSecurity()
        {
            //init at least a security group
            if (Groups.Count == 0)
            {
                SecurityGroup group = new SecurityGroup() { Name = "Default Group" };
                Groups.Add(group);
                group.Folders.Add(new SecurityFolder());
            }
        }

        /// <summary>
        /// Last modification date time
        /// </summary>
        [XmlIgnore]
        public DateTime LastModification;

        /// <summary>
        /// Load a SealSecurity from a file
        /// </summary>
        static public SealSecurity LoadFromFile(string path, bool ignoreException)
        {
            SealSecurity result = null;
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SealSecurity));
                using (XmlReader xr = XmlReader.Create(path))
                {
                    result = (SealSecurity)serializer.Deserialize(xr);
                }
                result.FilePath = path;
                result.LastModification = File.GetLastWriteTime(path);
                result.initSecurity();
            }
            catch (Exception ex)
            {
                if (!ignoreException) throw new Exception(string.Format("Unable to read the security file '{0}'.\r\n{1}", path, ex.Message));
                result = new SealSecurity();
                result.initSecurity();
            }
            return result;
        }


        /// <summary>
        /// Save to the current file
        /// </summary>
        public void SaveToFile()
        {
            SaveToFile(FilePath);
        }

        /// <summary>
        /// Save to a destination file path
        /// </summary>
        /// <param name="path"></param>
        public void SaveToFile(string path)
        {
            //Check last modification
            if (LastModification != DateTime.MinValue && File.Exists(path))
            {
                DateTime lastDateTime = File.GetLastWriteTime(path);
                if (LastModification != lastDateTime)
                {
                    throw new Exception("Unable to save the Security file. The file has been modified by another user.");
                }
            }
            XmlSerializer serializer = new XmlSerializer(typeof(SealSecurity));
            XmlWriterSettings ws = new XmlWriterSettings();
            ws.NewLineHandling = NewLineHandling.Entitize;
            using (XmlWriter xw = XmlWriter.Create(path, ws))
            {
                serializer.Serialize(xw, this);
            }
            FilePath = path;
            LastModification = File.GetLastWriteTime(path);
        }

        /// <summary>
        /// Last error message
        /// </summary>
        [XmlIgnore, Category("Helpers"), DisplayName("Error"), Description("Last error message."), Id(7, 11)]
        [EditorAttribute(typeof(ErrorUITypeEditor), typeof(UITypeEditor))]
        public string Error
        {
            get
            {
                string result = "";
                foreach (var provider in Providers.Where(i => !string.IsNullOrEmpty(i.Error)))
                {
                    result += string.Format("{0}: {1}\r\n", provider.Name, provider.Error);
                }
                return result;
            }
        }

        /// <summary>
        /// Editor Helper: User name to test a login
        /// </summary>
        [XmlIgnore, Category("Test a login"), DisplayName("User name for test"), Description("User name to test a login."), Id(8, 10)]
        public string TestUserName { get; set; }

        /// <summary>
        /// Editor Helper: Password to test a login
        /// </summary>
        [XmlIgnore, Category("Test a login"), PasswordPropertyText(true), DisplayName("Password for test"), Description("Password to test a login."), Id(9, 10)]
        public string TestPassword { get; set; }

        /// <summary>
        /// Editor Helper: If true, the current user will be use as IPrincipal to test the Integrated Windows authentication
        /// </summary>
        [XmlIgnore, Category("Test a login"), DisplayName("Use current Windows User to test authentication"), Description("If true, the current user will be use as IPrincipal to test the Integrated Windows authentication."), Id(10, 10)]
        public bool TestCurrentWindowsUser { get; set; } = true;

        /// <summary>
        /// Editor Helper: Test a login using the test user name and password or the current windows user
        /// </summary>
        [Category("Test a login"), DisplayName("Test a login"), Description("Test a login using the test user name and password or the current windows user."), Id(11, 10)]
        [Editor(typeof(HelperEditor), typeof(UITypeEditor))]
        public string HelperSimulateLogin
        {
            get { return "<Click to test a login>"; }
        }

        /// <summary>
        /// True if a parameter with a given name has a value
        /// </summary>
        public bool HasValue(string name)
        {
            return !string.IsNullOrEmpty(GetValue(name));
        }

        /// <summary>
        /// Parameter value of a given name
        /// </summary>
        public string GetValue(string name)
        {
            SecurityParameter parameter = CurrentParameters.FirstOrDefault(i => i.Name == name);
            return parameter == null ? "" : parameter.Value;
        }

        /// <summary>
        /// Parameter boolean value of a given name
        /// </summary>
        public bool GetBoolValue(string name)
        {
            SecurityParameter parameter = CurrentParameters.FirstOrDefault(i => i.Name == name);
            return parameter == null ? false : parameter.BoolValue;
        }

        /// <summary>
        /// Parameter numeric value of a given name
        /// </summary>
        public int GetNumericValue(string name)
        {
            SecurityParameter parameter = CurrentParameters.FirstOrDefault(i => i.Name == name);
            return parameter == null ? 0 : parameter.NumericValue;
        }

        /// <summary>
        /// List of logged users
        /// </summary>
        static public List<SecurityUser> LoggedUsers = new List<SecurityUser>();

        /// <summary>
        /// Add a logged user
        /// </summary>
        static public void AddLoggedUsed(SecurityUser user)
        {
            lock (LoggedUsers)
            {
                if (!LoggedUsers.Contains(user)) LoggedUsers.Add(user);
            }
        }

        /// <summary>
        /// Remove a logged user
        /// </summary>
        static public void RemoveLoggedUsed(SecurityUser user)
        {
            lock (LoggedUsers)
            {
                if (LoggedUsers.Contains(user)) LoggedUsers.Remove(user);
            }
        }
    }
}