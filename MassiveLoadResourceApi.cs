﻿using Gnoss.ApiWrapper.ApiModel;
using Gnoss.ApiWrapper.Exceptions;
using Gnoss.ApiWrapper.Helpers;
using Gnoss.ApiWrapper.Interfaces;
using Gnoss.ApiWrapper.Model;
using Gnoss.ApiWrapper.OAuth;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gnoss.ApiWrapper
{
    /// <summary>
    /// Resource Api for Massive Data Load
    /// </summary>
    public class MassiveLoadResourceApi : ResourceApi
    {
        private ILogHelper _logHelper;
       
        /// <summary>
        /// Massive data load identifier
        /// </summary>
        private Guid massiveLoadId;
        
        /// <summary>
        /// Massive data load name
        /// </summary>
        private string loadName;
        
        /// <summary>
        /// Directory path of the files
        /// </summary>
        private string filesDirectory;
        
        /// <summary>
        /// Massive load identifier
        /// </summary>
        public Guid MassiveLoadIdentifier { get; set; }
        
        /// <summary>
        /// Massive data load name
        /// </summary>
        public string LoadName { get; set; }
        
        /// <summary>
        /// Directory path of the files
        /// </summary>    
        public string FilesDirectory { get; set; }
        
        /// <summary>
        /// Number of resources and files
        /// </summary>
        public Dictionary<string, OntologyCount> counter = new Dictionary<string, OntologyCount>();
        
        /// <summary>
        /// Virtual directory of data
        /// </summary>
        public string Uri { get; set; }
        
        /// Max num of resources per packages
        /// </summary>
        private int MaxResourcesPerPackage { get; set; }

        private StreamWriter streamData;
        private StreamWriter streamOntology;
        private StreamWriter stremSearch;

        /// <summary>
        /// Constructor of <see cref="MassiveLoadResourceApi"/>
        /// </summary>
        /// <param name="communityShortName">Community short name which you want to use the API</param>
        /// <param name="oauth">OAuth information to sign the Api requests</param>
        /// <param name="developerEmail">(Optional) If you want to be informed of any incident that may happends during a large load of resources, an email will be sent to this email address</param>
        /// <param name="ontologyName">(Optional) Ontology name of the resources that you are going to query, upload or modify</param>
        public MassiveLoadResourceApi(OAuthInfo oauth, string communityShortName, IHttpContextAccessor httpContextAccessor, LogHelper logHelper, string ontologyName = null, string developerEmail = null, int maxResourcesPerPackage = 1000) : base(oauth, communityShortName, httpContextAccessor, logHelper, ontologyName, developerEmail)
        {
            MaxResourcesPerPackage = maxResourcesPerPackage;
            _logHelper = logHelper.Instance;
        }

        /// <summary>
        /// Consturtor of <see cref="MassiveLoadResourceApi"/>
        /// </summary>
        /// <param name="configFilePath">Configuration file path, with a structure like http://api.gnoss.com/v3/exampleConfig.txt </param>
        /// <param name="maxResourcesPerPackage">Num max of resources per package</param>
        public MassiveLoadResourceApi(OAuthInfo oauth, string configFilePath, IHttpContextAccessor httpContextAccessor, LogHelper logHelper, int maxResourcesPerPackage) : base(oauth, configFilePath, httpContextAccessor, logHelper)
        {
            MaxResourcesPerPackage = maxResourcesPerPackage;
            _logHelper = logHelper.Instance;
        }

        /// <summary>
        /// Consturtor of <see cref="MassiveLoadResourceApi"/>
        /// </summary>
        /// <param name="configFilePath">Configuration file path, with a structure like http://api.gnoss.com/v3/exampleConfig.txt </param>
        public MassiveLoadResourceApi(OAuthInfo oauth, string configFilePath, IHttpContextAccessor httpContextAccessor, LogHelper logHelper) : base(oauth, configFilePath, httpContextAccessor, logHelper)
        {
            _logHelper = logHelper.Instance;
        }

        /// <summary>
        /// Create a new massive data load
        /// </summary>
        /// <param name="pName">Massive data load name</param>
        /// <param name="pFilesDirectory">Path directory of the massive data load files</param>
        /// <param name="pOrganizationID">Organization identifier</param>
        /// <returns>Identifier of the load</returns>
        public Guid MassiveDataLoad(string pName, string pFilesDirectory, Guid? pOrganizationID = null)
        {
            try
            {
                FilesDirectory = pFilesDirectory;
                //Uri = "http://serviciostrygnoss.gnoss.com/massiveloadfiles/";
                Uri = "http://depuracion.net/files/";
                LoadName = pName;
                MassiveLoadIdentifier = Guid.NewGuid();
                CreateMassiveDataLoad(pOrganizationID);

                _logHelper.Debug($"Massive data load create with the identifier {MassiveLoadIdentifier}");
                return MassiveLoadIdentifier;
            }
            catch (Exception ex)
            {
                _logHelper.Error($"Error creating the massive data load {MassiveLoadIdentifier}{ex.Message} {ex.StackTrace}");
                throw new GnossAPIException($"Error creating the massive data load {MassiveLoadIdentifier}{ex.Message} {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Create a new package massive data load
        /// </summary>
        /// <param name="resource">Interface of the Gnoss Methods</param>
        /// <param name="isLast">The last resource</param>
        /// <returns>Identifier of the package</returns>
        public void MassiveDataLoadPackage(IGnossOCBase resource)
        {
            try
            {
                if (!counter.Keys.Contains(OntologyNameWithoutExtension))
                {
                    counter.Add(OntologyNameWithoutExtension, new OntologyCount(0, 0));
                }

                List<string> ontologyTriples = resource.ToOntologyGnossTriples(this);
                List<string> searchTriples = resource.ToSearchGraphTriples(this);
                KeyValuePair<Guid, string> acidData = resource.ToAcidData(this);

                string pathOntology = $"{FilesDirectory}\\{OntologyNameWithoutExtension}_{MassiveLoadIdentifier}_{counter[OntologyNameWithoutExtension].FileCount}.nq";
                string pathSearch = $"{FilesDirectory}\\{OntologyNameWithoutExtension}_search_{MassiveLoadIdentifier}_{counter[OntologyNameWithoutExtension].FileCount}.nq";
                string pathAcid = $"{FilesDirectory}\\{OntologyNameWithoutExtension}_acid_{MassiveLoadIdentifier}_{counter[OntologyNameWithoutExtension].FileCount}.txt";

                if (streamData == null || streamOntology == null || stremSearch == null)
                {
                    streamData = new StreamWriter(pathAcid);
                    streamOntology = new StreamWriter(pathOntology);
                    stremSearch = new StreamWriter(pathSearch);
                }

                foreach (string triple in ontologyTriples)
                {
                    streamOntology.WriteLine(triple);
                }
                foreach (string triple in searchTriples)
                {
                    stremSearch.WriteLine(triple);
                }
                streamData.WriteLine($"{acidData.Key}|||{acidData.Value}");

                if (counter[OntologyNameWithoutExtension].ResourcesCount >= MaxResourcesPerPackage)
                {
                    SendPackage();

                    counter[OntologyNameWithoutExtension].ResourcesCount = 0;
                    counter[OntologyNameWithoutExtension].FileCount++;
                }
                else
                {
                    counter[OntologyNameWithoutExtension].ResourcesCount++;
                }
            }
            catch (Exception ex)
            {
                _logHelper.Error($"Error creating the package of massive data load {ex.Message} {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Close a massive data load
        /// </summary>
        /// <returns>True if the data load is closed</returns>
        public bool CloseMassivaDataLoad()
        {
            string url = $"{ApiUrl}/resource/close-massive-load";
            CloseMassiveDataLoadResource model = null;
            bool closed = false;
            try
            {
                SendPackage();
                model = new CloseMassiveDataLoadResource()
                {
                    DataLoadIdentifier = MassiveLoadIdentifier
                };
                WebRequestPostWithJsonObject(url, model);
                _logHelper.Debug("Data load is closed");
                closed = true;
            }
            catch (Exception ex)
            {
                _logHelper.Error($"Error closing the data load {MassiveLoadIdentifier}. \r\n Json: {JsonConvert.SerializeObject(model)}", ex.Message);
                throw;
            }

            return closed;
        }

        /// <summary>
        /// Create the massive data load
        /// <param name="organizationID">OPTIONAL: Organization identifier. If is not write, is '11111111-1111-1111-1111-111111111111'</param>
        /// <returns>True if the load is correctly created</returns>
        /// </summary>
        private bool CreateMassiveDataLoad(Guid? organizationID = null)
        {
            bool created = false;
            MassiveDataLoadResource model = null;
            if (organizationID == null)
            {
                organizationID = new Guid("11111111-1111-1111-1111-111111111111");
            }
            try
            {
                string url = $"{ApiUrl}/resource/create-massive-load";
                model = new MassiveDataLoadResource()
                {
                    load_id = MassiveLoadIdentifier,
                    name = LoadName,
                    community_name = CommunityShortName,
                    oganization_id = organizationID.Value
                };
                WebRequestPostWithJsonObject(url, model);
                created = true;
                _logHelper.Debug("Massive data load created");
            }
            catch (Exception ex)
            {
                _logHelper.Error($"Error creating massive data load {MassiveLoadIdentifier}. \r\n Json: {JsonConvert.SerializeObject(model)}", ex.Message);
                throw;
            }

            return created;
        }

        private void SendPackage()
        {
            try
            {
                string uriOntology = $"{Uri}/{OntologyNameWithoutExtension}_{MassiveLoadIdentifier}_{counter[OntologyNameWithoutExtension].FileCount}.nq";
                string uriSearch = $"{Uri}/{OntologyNameWithoutExtension}_search_{MassiveLoadIdentifier}_{counter[OntologyNameWithoutExtension].FileCount}.nq";
                string uriAcid = $"{Uri}/{OntologyNameWithoutExtension}_acid_{MassiveLoadIdentifier}_{counter[OntologyNameWithoutExtension].FileCount}.txt";

                MassiveDataLoadPackageResource model = new MassiveDataLoadPackageResource();
                model.package_id = Guid.NewGuid();
                model.load_id = MassiveLoadIdentifier;
                model.ontology_rute = uriOntology;
                model.search_rute = uriSearch;
                model.sql_rute = uriAcid;
                model.ontology = OntologyUrl;
                model.isLast = false;

                CreatePackageMassiveDataLoad(model);

                CloseStreams();

                _logHelper.Debug($"Package massive data load create with the identifier {MassiveLoadIdentifier}");
            }
            catch (Exception ex)
            {
                _logHelper.Error($"Error creating the package of massive data load {ex.Message}");
            }
        }

        private void CloseStreams()
        {
            streamData.Flush();
            streamData.Close();
            streamData = null;

            streamOntology.Flush();
            streamOntology.Close();
            streamOntology = null;

            stremSearch.Flush();
            stremSearch.Close();
            stremSearch = null;
        }

        /// <summary>
        /// Creates a new package in a massive data load
        /// </summary>
        /// <param name="model">Data model of the package massive data load</param>       
        /// <returns>True if the new massive data load package was created succesfully</returns>
        private bool CreatePackageMassiveDataLoad(MassiveDataLoadPackageResource model)
        {
            bool created = false;
            try
            {
                string url = $"{ApiUrl}/resource/create-massive-load-package";
                WebRequestPostWithJsonObject(url, model);
                created = true;
                _logHelper.Debug("Massive data load package created");
            }
            catch (Exception ex)
            {
                _logHelper.Error($"Error creating massive data load package {model.package_id}. \r\n Json: {JsonConvert.SerializeObject(model)}", ex.Message);
                throw;
            }

            return created;
        }
    }
}
