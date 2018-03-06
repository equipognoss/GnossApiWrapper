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
    /// Wrapper for GNOSS notification API
    /// </summary>
    public class NotificationApi : GnossApiWrapper
    {
        #region Constructors

        /// <summary>
        /// Constructor of <see cref="NotificationApi"/>
        /// </summary>
        /// <param name="oauth">OAuth information to sign the Api requests</param>
        public NotificationApi(OAuthInfo oauth) : base(oauth)
        {

        }

        /// <summary>
        /// Consturtor of <see cref="NotificationApi"/>
        /// </summary>
        /// <param name="configFilePath">Configuration file path, with a structure like http://api.gnoss.com/v3/exampleConfig.txt </param>
        public NotificationApi(string configFilePath) : base(configFilePath)
        {

        }

        #endregion

        #region Public methods

        /// <summary>
        /// Send an e-mail notification
        /// </summary>
        /// <param name="subject">Subject of the notification</param>
        /// <param name="message">Message of the notification</param>
        /// <param name="isHTML">It indicates whether the content is html</param>
        /// <param name="receivers">Receivers of the notification</param>
        /// <param name="senderMask">Mask sender of the notification</param>
        public void SendEmail(string subject, string message, List<string> receivers, bool isHTML = false, string senderMask = "")
        {
            try
            {
                string url = $"{ApiUrl}/notification/send-email";

                NotificationModel model = new NotificationModel() { subject = subject, message = message, receivers = receivers, is_html = isHTML, sender_mask = senderMask};

                WebRequestPostWithJsonObject(url, model);

                LogHelper.Instance.Debug($"Email {subject} sended to {string.Join(",", receivers)}");
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Error($"Error sending mail {subject} to {string.Join(",", receivers)}: \r\n{ex.Message}");
                throw;
            }
        }

        #endregion
    }
}