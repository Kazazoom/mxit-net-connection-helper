/// Mxit-Net-Connection-Helper
/// Author: Eric Clements (Kazazoom) - eric@kazazoom.com
/// License: BSD-3 (https://github.com/Kazazoom/mxit-net-connection-helper/blob/master/license.txt)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MXitConnectionModule
{
    internal static class RESTMxitResponseCode
    {
        internal const String OK = "200";
        internal const String WebContextNull = "203";
        internal const String OAuthTokenInvalidOrExpired = "401";
        internal const String OAuthTokenPermissionDeniedForMethod = "403";
        internal const String MethodNotAllowed = "405";
        internal const String InternalError = "500";
    }


    //public enum GenderType
    //{
    //    OK = 200,
    //    WebContextNull = 203,
    //}
}
