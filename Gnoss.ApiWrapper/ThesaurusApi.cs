﻿using Gnoss.ApiWrapper.ApiModel;
using Gnoss.ApiWrapper.Helpers;
using Gnoss.ApiWrapper.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gnoss.ApiWrapper
{
    /// <summary>
    /// Wrapper for GNOSS thsaurus API
    /// </summary>
    public class ThesaurusApi : GnossApiWrapper
    {

        #region Constructors

        /// <summary>
        /// Constructor of <see cref="ThesaurusApi"/>
        /// </summary>
        /// <param name="communityShortName">Community short name which you want to use the API</param>
        /// <param name="oauth">OAuth information to sign the Api requests</param>
        public ThesaurusApi(OAuthInfo oauth, string communityShortName) : base(oauth, communityShortName)
        {

        }

        /// <summary>
        /// Consturtor of <see cref="ThesaurusApi"/>
        /// </summary>
        /// <param name="configFilePath">Configuration file path, with a structure like http://api.gnoss.com/v3/exampleConfig.txt </param>
        public ThesaurusApi(string configFilePath) : base(configFilePath)
        {

        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Get the RDF of a semantic thesaurus
        /// </summary>
        /// <param name="thesaurusOntologyUrl">Ontology URL of the thesaurus</param>
        /// <param name="source">Identifier of the thesaurus</param>
        /// <returns>RDF of a semantic thesaurus</returns>
        public string GetThesaurus(string thesaurusOntologyUrl, string source)
        {
            try
            {
                string url = $"{ApiUrl}/thesaurus/get-thesaurus?thesaurus_ontology_url={thesaurusOntologyUrl}&community_short_name={CommunityShortName}&source={source}";

                string response = WebRequest("GET", url, acceptHeader: "application/json")?.Trim('"');

                if (!string.IsNullOrEmpty(response))
                {
                    LogHelper.Instance.Debug($"Thesaurus obtained successfully");
                }

                return response;
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Error($"There has been an error getting the thesaurus of the community {CommunityShortName} and ontology {thesaurusOntologyUrl}. {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Moves a category of a semantic thesaurus from its current father to another one, indicating its full path from the root.
        /// </summary>
        /// <param name="pUrlOntologiaTesauro">URL of the semantic thesaurus ontology</param>
        /// <param name="pUrlOntologiaRecursos">URL of the ontology of the resources that are linked to the semantic thesaurus</param>
        /// <param name="pCategoriaAMoverId">URI of the category to move</param>
        /// <param name="pPath">Path from the root to the last new father of the category</param>
        public void MoveSemanticThesaurusNode(string pUrlOntologiaTesauro, string pUrlOntologiaRecursos, string pCategoriaAMoverId, string[] pPath)
        {
            try
            {
                string url = $"{ApiUrl}/thesaurus/move-node";

                ParamsMoveNode model = new ParamsMoveNode() { thesaurus_ontology_url = pUrlOntologiaTesauro, resources_ontology_url = pUrlOntologiaRecursos, community_short_name = CommunityShortName, category_id = pCategoriaAMoverId, path = pPath };

                WebRequestPostWithJsonObject(url, model);

                LogHelper.Instance.Debug($"The category {pCategoriaAMoverId} has been moved");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Removes a category of a semantic thesaurus moving all resources that were linked to it to another one indicating its complete path from the root.
        /// </summary>
        /// <param name="pUrlOntologiaTesauro">URL of the semantic thesaurus ontology</param>
        /// <param name="pUrlOntologiaRecursos">URL of the ontology of the resources that are linked to the semantic thesaurus</param>
        /// <param name="pCategoriaAEliminarId">URI of the category to remove</param>
        /// <param name="pPath">Path from the root father to its last child where resources of the removed category are going to be moved to</param>
        public void RemoveSemanticThesaurusNode(string pUrlOntologiaTesauro, string pUrlOntologiaRecursos, string pCategoriaAEliminarId, string[] pPath)
        {
            try
            {
                string url = $"{ApiUrl}/thesaurus/delete-node";

                ParamsMoveNode model = new ParamsMoveNode() { thesaurus_ontology_url = pUrlOntologiaTesauro, resources_ontology_url = pUrlOntologiaRecursos, community_short_name = CommunityShortName, category_id = pCategoriaAEliminarId, path = pPath };

                WebRequestPostWithJsonObject(url, model);

                LogHelper.Instance.Debug($"The category {pCategoriaAEliminarId} has been deleted");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Adds a category as a parent of another one.
        /// </summary>
        /// <param name="pUrlOntologiaTesauro">URL of the semantic thesaurus ontology</param>
        /// <param name="pCategoriaPadreId">URI of the parent category</param>
        /// <param name="pCategoriaHijoId">URI of the child category</param>
        public void AddFatherToSemanticThesaurusNode(string pUrlOntologiaTesauro, string pCategoriaPadreId, string pCategoriaHijoId)
        {
            try
            {
                string url = $"{ApiUrl}/thesaurus/set-node-parent";

                ParamsParentNode model = new ParamsParentNode() { thesaurus_ontology_url = pUrlOntologiaTesauro, community_short_name = CommunityShortName, parent_catergory_id = pCategoriaPadreId, child_category_id = pCategoriaHijoId };

                WebRequestPostWithJsonObject(url, model);

                LogHelper.Instance.Debug($"The parent of the category {pCategoriaHijoId} is now {pCategoriaPadreId}");
            }
            catch (Exception ex)
            {
                throw (ex);
            }
        }

        /// <summary>
        /// Modifies the semantic thesaurus category name
        /// </summary>
        /// <param name="pUrlOntologiaTesauro">URL of the semantic thesaurus ontology</param>
        /// <param name="pCategoriaId">URI of the category</param>
        /// <param name="pNombre">Category name, supports multi language with the format: nombre@es|||name@en|||</param>
        public void ChangeNameToSemanticThesaurusNode(string pUrlOntologiaTesauro, string pCategoriaId, string pNombre)
        {
            try
            {
                string url = $"{ApiUrl}/thesaurus/change-node-name";

                ParamsChangeName model = new ParamsChangeName() { thesaurus_ontology_url = pUrlOntologiaTesauro, community_short_name = CommunityShortName, category_id = pCategoriaId, category_name = pNombre };

                WebRequestPostWithJsonObject(url, model);

                LogHelper.Instance.Debug($"The category {pCategoriaId} has change, and now is {pNombre}");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Inserts a category of a semantic thesaurus.
        /// </summary>
        /// <param name="pUrlOntologiaTesauro">URL of the semantic thesaurus ontology</param>
        /// <param name="pRdfCategoria">Inserted category Rdf</param>
        public void InsertSemanticThesaurusNode(string pUrlOntologiaTesauro, byte[] pRdfCategoria)
        {
            try
            {
                string url = $"{ApiUrl}/thesaurus/change-node-name";

                ParamsInsertNode model = new ParamsInsertNode() { thesaurus_ontology_url = pUrlOntologiaTesauro, community_short_name = CommunityShortName, rdf_category = pRdfCategoria };

                WebRequestPostWithJsonObject(url, model);

                LogHelper.Instance.Debug($"A semantic category has been added to the community {CommunityShortName}");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #endregion
    }
}
