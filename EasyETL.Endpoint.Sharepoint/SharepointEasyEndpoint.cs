﻿using EasyETL.Attributes;
using EasyETL.Endpoint;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using SP = Microsoft.SharePoint.Client;

namespace EasyEndpoint
{
    [DisplayName("Sharepoint")]
    [EasyProperty("CanListen", "False")]
    [EasyProperty("HasFiles", "True")]
    [EasyProperty("CanStream", "True")]
    [EasyProperty("CanRead", "True")]
    [EasyProperty("CanWrite", "True")]
    [EasyProperty("CanList", "True")]
    [EasyField("SiteName", "Name of the Site")]
    [EasyField("LibraryName", "Name of the Document Library")]
    [EasyField("UserName", "UserName to access the site")]
    [EasyField("UserPassword", "Password of the username to access the Site","","","",true)]
    [EasyField("Overwrite", "Can Overwrite files if already present ?", "False", "True|False", "True;False")]
    public class SharepointEasyEndpoint : AbstractFileEasyEndpoint, IDisposable
    {
        string SiteName = "";
        ICredentials Credentials = null;
        string LibraryName = String.Empty;
        string UserName = String.Empty;
        String UserPassword = String.Empty;
        SP.ClientContext _clientContext = null;
        SP.Web _web = null;
        SP.Site _site = null;

        public SharepointEasyEndpoint() : base()
        {

        }

        public SharepointEasyEndpoint(string siteName, string userName, string password, string libraryName, bool overwriteFiles = true)
        {
            SiteName = siteName;
            if (userName.Contains('@'))
            {
                Credentials = new SP.SharePointOnlineCredentials(userName, GetSecureString(password));
            }
            else
            {
                Credentials = GetCredential(userName, password);
            }

            LibraryName = libraryName;
            Overwrite = overwriteFiles;
        }


        #region Public overriden methods
        public override string[] GetList(string filter = "*.*")
        {
            List<string> resultList = new List<string>();
            try
            {
                SP.ListItemCollection spListItems = SharepointList.GetItems(SP.CamlQuery.CreateAllItemsQuery(20, "Title"));
                SharepointClientContext.Load(spListItems);
                SharepointClientContext.ExecuteQuery();
                foreach (SP.ListItem spListItem in spListItems)
                {
                    string fileName = spListItem.FieldValues["FileLeafRef"].ToString();
                    if (Operators.LikeString(fileName, filter, CompareMethod.Text))
                    {
                        resultList.Add(fileName);
                    }
                }
            }
            catch
            {
            }
            return resultList.ToArray();
        }

        public override Stream GetStream(string fileName)
        {
            try
            {
                if (TryGetFileByServerRelativeUrl(_web, _web.ServerRelativeUrl + "/" + LibraryName + "/" + fileName, out SP.File spFile))
                {
                    SharepointClientContext.Load(spFile);
                    SharepointClientContext.ExecuteQuery();
                    using (SP.FileInformation fileInfo = SP.File.OpenBinaryDirect(_clientContext, spFile.ServerRelativeUrl))
                    {
                        return fileInfo.Stream;
                    }
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        public override byte[] Read(string fileName)
        {
            try
            {
                Stream stream = GetStream(fileName);
                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        public override bool Write(string fileName, byte[] data)
        {
            try
            {
                SharepointClientContext.Load(SharepointList.RootFolder);
                SharepointClientContext.ExecuteQuery();

                string fileUrl = String.Format("{0}/{1}", SharepointList.RootFolder.ServerRelativeUrl, fileName);
                Microsoft.SharePoint.Client.File.SaveBinaryDirect(_clientContext, fileUrl, new MemoryStream(data, false), true);
                _clientContext.ExecuteQuery(); //Uploaded .. but still checked out...


                //Load the FieldCollection from the list...
                SP.FieldCollection fileFields = SharepointList.Fields;
                _clientContext.Load(fileFields);
                _clientContext.ExecuteQuery();

                SP.File uploadedFile = _web.GetFileByServerRelativeUrl(fileUrl);
                SP.ListItem newFileListItem = uploadedFile.ListItemAllFields;
                newFileListItem.Update();
                _clientContext.ExecuteQuery();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool FileExists(string fileName)
        {
            return TryGetFileByServerRelativeUrl(_web, _web.ServerRelativeUrl + "/" + LibraryName + "/" + fileName, out _);
        }

        public override bool Delete(string fileName)
        {
            try
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override void LoadSetting(string fieldName, string fieldValue)
        {
            base.LoadSetting(fieldName, fieldValue);
            switch (fieldName.ToLower())
            {
                case "username":
                    UserName = fieldValue; break;
                case "userpassword":
                    UserPassword = fieldValue; break;
                case "libraryname":
                    LibraryName = fieldValue; break;
                case "sitename":
                    SiteName = fieldValue; break;
            }
        }

        public override Dictionary<string, string> GetSettingsAsDictionary()
        {
            Dictionary<string,string> resultDict = base.GetSettingsAsDictionary();
            resultDict.Add("username", UserName);
            resultDict.Add("userpassword", UserPassword);
            resultDict.Add("libraryname", LibraryName);
            resultDict.Add("sitename", SiteName);
            return resultDict;
        }

        public override bool IsFieldSettingsComplete()
        {
            return base.IsFieldSettingsComplete()  && !String.IsNullOrWhiteSpace(UserName) && !String.IsNullOrWhiteSpace(UserPassword);
        }

        public override bool CanFunction()
        {
            if (!IsFieldSettingsComplete()) return false;
            return (SharepointClientContext != null);
        }


        #endregion

        #region Private Methods

        private static bool TryGetFileByServerRelativeUrl(SP.Web web, string serverRelativeUrl, out SP.File file)
        {
            var ctx = web.Context;
            try
            {
                file = web.GetFileByServerRelativeUrl(serverRelativeUrl);
                ctx.Load(file);
                ctx.ExecuteQuery();
                return file.Exists;
            }
            catch
            {
                file = null;
                return false;
            }
        }

        #endregion

        #region Sharepoint specific properties
        public SP.ClientContext SharepointClientContext
        {
            get
            {
                if (_clientContext == null)
                {
                    //Initialize and load the Client Context...
                    _clientContext = new SP.ClientContext(SiteName);
                    if (!String.IsNullOrWhiteSpace(UserName))
                    {
                        Credentials = GetCredential(UserName, UserPassword);
                    }
                    _clientContext.Credentials = Credentials;
                    _clientContext.ExecuteQuery();

                    //Load the Web
                    _web = _clientContext.Web;
                    _clientContext.Load(_web);
                    _clientContext.ExecuteQuery();

                    //Load the Site....
                    _site = _clientContext.Site;
                    _clientContext.Load(_site);
                    _clientContext.ExecuteQuery();
                }
                return _clientContext;
            }
        }

        public SP.Web SharepointWeb
        {
            get
            {
                if (_web == null) _web = SharepointClientContext.Web;
                return _web;
            }
        }

        public SP.Site SharepointSite
        {
            get
            {
                if (_site == null) _site = SharepointClientContext.Site;
                return _site;
            }
        }

        public SP.List SharepointList
        {
            get
            {
                try
                {
                    SP.ListCollection spLists = SharepointWeb.Lists;
                    SharepointClientContext.Load(spLists);
                    SharepointClientContext.ExecuteQuery();

                    SP.List spList = spLists.GetByTitle(LibraryName);
                    SharepointClientContext.Load(spList);
                    SharepointClientContext.ExecuteQuery();
                    return spList;
                }
                catch
                {
                    return null;
                }
            }
        }
        
        #endregion

        public void Dispose()
        {
            if (_site != null) _site = null;
            if (_web != null) _web = null;
            if (_clientContext != null)
            {
                _clientContext.Dispose();
                _clientContext = null;
            }
            Credentials = null;
        }
    }
}
