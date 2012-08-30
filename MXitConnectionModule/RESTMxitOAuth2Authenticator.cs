/// Mxit-Net-Connection-Helper
/// Author: Eric Clements (Kazazoom) - eric@kazazoom.com
/// License: BSD-3 (https://github.com/Kazazoom/mxit-net-connection-helper/blob/master/license.txt)
/// Credits: This RESTMxitOAuth2Authenticator class approach figured out by George Ziady (Springfisher) - george@springfisher.com

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RestSharp;

namespace MXitConnectionModule
{
    public class RESTMxitOAuth2Authenticator : OAuth2Authenticator
    {
        // This authenticate extends the OAuth2UriQueryParameterAuthenticator        
        public RESTMxitOAuth2Authenticator(string accessToken) : base(accessToken)
        {
        }

        public override void Authenticate(IRestClient client, IRestRequest request)
        {
            //MXit requests that the authorize parameter is added in the following form
            request.AddHeader("Authorization", "Bearer " + AccessToken);
        }
    }
}
