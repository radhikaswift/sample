/* 
 * Copyright (c) 2013-2017 BlackBerry. 
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License. 
 * You may obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software 
 * distributed under the License is distributed on an "AS IS" BASIS, 
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
 * See the License for the specific language governing permissions and 
 * limitations under the License. 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Net;
using System.IO;


/*
 * AuthenticationSample.cs
 *
 * A program that demonstrates the different methods of authentication for 
 * BlackBerry Web Services (BWS) for Enterprise Administration APIs.
 *
 * This sample program demonstrates how to make an authenticated API call
 * using one of three options:
 *	1)	User Credentials (i.e. non-directory users)
 *	2)  Active Directory Credentials
 *	3)	Single Sign On using the currently logged in user's credentials
 *
 * This program was tested using .NET framework 4
 * 
 * This program was tested against the BlackBerry Enterprise Service 10 version 10.2.0.
 * This program was tested against the BlackBerry Enterprise Service 12 version 12.0.0.
 * This program was tested against the BlackBerry Unified Endpoint Management version 12.6.0
 */

namespace CodeSample
{
    class AuthenticationSample
    {
        // Timer used by logging.
        private static Stopwatch startTime = new Stopwatch();

        // Web service stubs.
        private static BWSService bwsService;
        private static BWSUtilService bwsUtilService;

        // The request Metadata information.
        // This is the version of the WSDL used to generate the proxy, not the version of the server.	
        private const string CLIENT_VERSION = "<Client Version>"; // e.g. CLIENT_VERSION = "12.6.0"

        // The enum used to determine the current server type.
        private enum ServerType { Unknown, BDS, UDS, BES12 } ;

        // Enum used to determine the server used in this execution
        private static ServerType serverType = ServerType.Unknown;

        /*
         * To use a different locale, call getLocales() in the BWSUtilService web service
         * to see which locales are supported. 
         */
        private const string LOCALE = "en_US";
        private const string ORG_UID = "0";
        private static readonly RequestMetadata REQUEST_METADATA = new RequestMetadata();

        /// <summary>
        /// Get the authenticator object for the authenticator name.
        /// </summary>
        /// <param name="authenticatorName">A string containing the name of the desired authenticator.</param>
        /// <returns>Returns the requested authenticator if it is found, and null otherwise.</returns>
        public static Authenticator getAuthenticator(String authenticatorName)
        {
            const string METHOD_NAME = "getAuthenticator()";
            const string BWS_API_NAME = "bwsUtilService.getAuthenticators()";
            logMessage("Entering {0}", METHOD_NAME);
            Authenticator returnValue = null;

            GetAuthenticatorsRequest request = new GetAuthenticatorsRequest();
            request.metadata = REQUEST_METADATA;

            GetAuthenticatorsResponse response = null;

            try
            {
                logRequest(BWS_API_NAME);
                response = bwsUtilService.getAuthenticators(request);
                logResponse(BWS_API_NAME, response.returnStatus.code, response.metadata);
            }
            catch (WebException e)
            {
                // Log and re-throw exception.
                logMessage("Exiting {0} with exception \"{1}\"", METHOD_NAME, e.Message);
                throw e;
            }

            if (response.returnStatus.code.Equals("SUCCESS"))
            {
                if (response.authenticators != null && response.authenticators.Length > 0)
                {
                    foreach (Authenticator authenticator in response.authenticators)
                    {
                        if (authenticator.name.Equals(authenticatorName, StringComparison.CurrentCultureIgnoreCase))
                        {
                            returnValue = authenticator;
                            break;
                        }
                    }

                    if (returnValue == null)
                    {
                        logMessage("Could not find \"{0}\" in GetAuthenticatorsResponse",
                            authenticatorName);
                    }
                }
                else
                {
                    logMessage("No authenticators in GetAuthenticatorsResponse");
                }
            }
            else
            {
                logMessage("Error Message: \"{0}\"", response.returnStatus.message);
            }

            logMessage("Exiting {0} with {1}", METHOD_NAME, returnValue == null ? "\"null\"" :
                "Authenticator object (Name \"" + returnValue.name + "\")");
            return returnValue;
        }

        /// <summary>
        /// Get the encoded username required to authenticate user to BWS.
        /// </summary>
        /// <returns>Returns a string containing the encoded username if successful, and a null otherwise.</returns>
        public static string getEncodedUserName(String username, Authenticator authenticator,
            CredentialType credentialType, String domain)
        {
            const string METHOD_NAME = "getEncodedUserName()";
            const string BWS_API_NAME = "bwsUtilService.getEncodedUsername()";
            logMessage("Entering {0}", METHOD_NAME);
            string returnValue = null;

            GetEncodedUsernameRequest request = new GetEncodedUsernameRequest();
            request.metadata = REQUEST_METADATA;
            request.username = username;
            request.orgUid = REQUEST_METADATA.organizationUid;
            request.authenticator = authenticator;

            request.credentialType = credentialType;
            request.domain = domain;

            GetEncodedUsernameResponse response = null;
            try
            {
                logRequest(BWS_API_NAME);
                response = bwsUtilService.getEncodedUsername(request);
                logResponse(BWS_API_NAME, response.returnStatus.code, response.metadata);
            }
            catch (WebException e)
            {
                // Log and re-throw exception.
                logMessage("Exiting {0} with exception \"{1}\"", METHOD_NAME, e.Message);
                throw e;
            }

            if (response.returnStatus.code.Equals("SUCCESS"))
            {
                returnValue = response.encodedUsername;
            }
            else
            {
                logMessage("Error Message: \"{0}\"", response.returnStatus.message);
            }

            // BES12/UEM returns a Base64 encoded string while BES10 does not.  As a result, a BES10 encoded username
            // throws an exception when an attempt is made to decode from base64.  The try block will log the decoded
            // username if run against a BES12/UEM system and the catch then logs the actual value returned by BES10.
            try
            {
                logMessage("Decoded value of encoded username \"{0}\"",
                    Encoding.Default.GetString(Convert.FromBase64String(returnValue)));
            }
            catch (Exception)
            {
                // Not actually base64 encoded. Show actual value
                logMessage("Value of encoded username \"{0}\"", returnValue);
            }
            logMessage("Exiting {0}", METHOD_NAME);
            return returnValue;
        }
        /// <summary>
        /// Call bwsService.getSystemInfo() and set the serverType member.
        /// </summary>
        public static void GetSystemInfo()
        {
            const string methodName = "GetSystemInfo()";
            const string bwsApiName = "bwsService.getSystemInfo()";

            logMessage("Entering {0}", methodName);

            GetSystemInfoRequest request = new GetSystemInfoRequest();

            request.metadata = REQUEST_METADATA;

            GetSystemInfoResponse response = null;

            /* 
             * The try catch block here is used to illustrate how to handle a specific type of exception.
             * For example, in this case we check to see if the error was caused by invalid credentials.
             */
            try
            {
                logRequest(bwsApiName);
                response = bwsService.getSystemInfo(request);
                logResponse(bwsApiName, response.returnStatus.code, response.metadata);
            }
            catch (WebException e)
            {
                HttpWebResponse webResponse = e.Response as HttpWebResponse;
                // Handle authentication failure.
                if (webResponse != null && webResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    logMessage("Failed to authenticate with the BWS web service");
                }

                // Log and re-throw exception.
                logMessage("Exiting {0} with exception \"{1}\"", methodName, e.Message);
                throw e;
            }

            if (response.returnStatus.code.Equals("SUCCESS"))
            {
                if (response.properties != null && response.properties.Length > 0)
                {
                    foreach (Property property in response.properties)
                    {
                        if (property.name.ToUpper().Equals("BAS VERSION"))
                        {
                            if (property.value.Split('.')[0].Equals("12"))
                            {
                                serverType = ServerType.BES12;
                                logMessage("ServerType found: BES12/UEM");
                            }
                            else
                            {
                                serverType = ServerType.BDS;
                                logMessage("ServerType found: BDS");
                            }
                            break;
                        }
                        if (property.name.ToUpper().Equals("BUDS VERSION"))
                        {
                            serverType = ServerType.UDS;
                            logMessage("ServerType found: UDS");
                            break;
                        }
                    }
                }
                else
                {
                    logMessage("No properties in response");
                }
            }
            else
            {
                logMessage("Error Message: \"{0}\"", response.returnStatus.message);
            }

            logMessage("Exiting {0}", methodName);
        }

        /// <summary> Perform a call to bwsService.echo(). </summary>
        /// <returns>Returns true if echo is successful, and false otherwise.</returns>
        private static bool echo()
        {
            const string METHOD_NAME = "echo()";
            const string BWS_API_NAME = "bwsService.echo()";
            logMessage("Entering {0}", METHOD_NAME);

            bool returnValue = true;

            EchoRequest request = new EchoRequest();
            EchoResponse response = null;

            request.metadata = REQUEST_METADATA;
            request.text = "Hello World!";

            try
            {
                logRequest(BWS_API_NAME);
                response = bwsService.echo(request);
                logResponse(BWS_API_NAME, response.returnStatus.code, response.metadata);
            }
            catch (WebException e)
            {
                if (e.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse httpWebResponse = (HttpWebResponse)e.Response;
                    if (httpWebResponse != null
                            && httpWebResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        returnValue = false;
                        logMessage("Failed to authenticate with the BWS web service");
                        logMessage("Exiting {0} with value \"{1}\"", METHOD_NAME, returnValue);
                        return returnValue;
                    }

                }

                // Log and re-throw exception.
                logMessage("Exiting {0} with exception \"{1}\"", METHOD_NAME, e.Message);
                throw e;
            }

            logMessage("Exiting {0} with value \"{1}\"", METHOD_NAME, returnValue);
            return returnValue;
        }

        /// <summary>
        /// Creates a string containing the elapsed time since the program started.
        /// The execution time will be reset to 00:00.000 if the execution time exceeds an hour. 
        /// </summary>
        /// <returns>Returns the elapsed time from start of program.</returns>
        public static String logTime()
        {
            String time = startTime.Elapsed.ToString();
            // trim decimals to 3 digits for seconds
            time = time.Substring(0, time.IndexOf('.') + 4);
            // get rid of HH:
            time = time.Substring(3);
            return time;
        }

        /// <summary> Prints a log message to stderr.</summary>
        /// <param name="format">A string which formats how args will be displayed in the message.</param>
        /// <param name="args">List of objects to be displayed in the message.</param>
        public static void logMessage(String format, params Object[] args)
        {   //Change output stream if desired
            TextWriter logStream = Console.Error;
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            logStream.WriteLine(logTime() + " " + format, args);
        }

        /// <summary>
        /// Logs the calling of an API.
        /// </summary>
        public static void logRequest(String bwsApiName)
        {
            logMessage("Calling {0}...", bwsApiName);
        }

        /// <summary>
        /// Logs various information about an API response.
        /// </summary>
        public static void logResponse(String bwsApiName, String code, ResponseMetadata metadata)
        {
            logMessage("...{0} returned \"{1}\"", bwsApiName, code);
            if (metadata != null)
            {
                /* 
                 * Converting response.metadata.executionTime (which is in nano-seconds) into seconds by 
                 * multiplying it by 10^-9.
                 */
                logMessage("Execution Time: {0:0.0000} seconds", (metadata.executionTime * Math.Pow(10, -9)));
                logMessage("Request UID: {0}", metadata.requestUid);
            }
        }

        /// <summary>
        /// The Main method.        
        /// </summary>
        /// <param name="args">Not used.</param>
        static int Main(string[] args)
        {
            startTime.Start();

            // Return codes.
            const int SUCCESS = 0;
            const int FAILURE = 1;

            int returnCode = SUCCESS;

            // Hostname to use when connecting to web service. Must contain the fully qualified domain name.
            String bwsHostname = "<bwsHostname>"; // e.g. bwsHostname = "server01.example.net"

            // Port to use when connecting to web service. The same port is used to access the
            // webconsole prior to BES12/UEM
            String bwsPort = "<bwsPort>"; // e.g. bwsPort = "18084"

            // Credeitnal type used in the authentication process
            CredentialType credentialType = new CredentialType();
            credentialType.PASSWORD = true;
            credentialType.value = "PASSWORD";

            /* 
             * BWS Host certificate must be installed on the client machine before running this sample code, otherwise
             * a SSL/TLS secure channel error will be thrown. For more information, see the BlackBerry Web Services 
			 * for Enterprise Administration For Microsoft .NET Developers Getting Started Guide.
             * 
             * To test authentication populate the methods below with the appropriate credentials and
             * information
             */

            // Select which authentication methods you would like to test by setting the variables to true
            bool useAD = true; // Active Directory
            bool useLDAP = true; // LDAP

            try
            {
                if (bwsHostname.IndexOf('.') < 1)
                {
                    throw new Exception(
                       "Invalid bwsHostname format. Expected format is \"server01.example.net\"");
                }
                // bwsPort, if not null, must be a positive integer
                if (bwsPort != null)
                {
                    int port = Int32.Parse(bwsPort);

                    if (port < 1)
                    {
                        throw new Exception("Invalid bwsPort. Expecting a positive integer string or null");
                    }
                }

                returnCode = (demonstrateBlackBerryAdministrationServiceAuthentication(bwsHostname,
                        bwsPort, credentialType)) ? SUCCESS : FAILURE;
                logMessage("");

                if (useAD && returnCode == SUCCESS)
                {
                    returnCode = (demonstrateActiveDirectoryAuthentication(bwsHostname, bwsPort,
                        credentialType)) ? SUCCESS : FAILURE;
                    logMessage("");
                }
                if (useLDAP && returnCode == SUCCESS)
                {
                    returnCode = (demonstrateLDAPAuthentication(bwsHostname, bwsPort, credentialType))
                        ? SUCCESS : FAILURE;
                    logMessage("");
                }
            }
            catch (Exception e)
            {
                logMessage("Exception: \"{0}\"\n", e.Message);
                returnCode = FAILURE;
            }

            Console.Error.WriteLine("Exiting sample.\nPress Enter to exit\n");
            Console.ReadKey();

            return returnCode;
        }

        /// <summary>
        /// Demonstrates BlackBerry Administration Service Authentication. Fields denoted by "<value>" 
        /// must be manually set.
        /// </summary>
        /// 
        /// <returns>Returns true if authenticated successfully, false otherwise.</returns>
        private static bool demonstrateBlackBerryAdministrationServiceAuthentication(String bwsHostname,
            String bwsPort, CredentialType credentialType)
        {
            logMessage("Attempting BlackBerry Administration Service authentication");

            // The BlackBerry Administration Service Credentials to use
            String username = "<username>"; // e.g. username = "admin".
            String password = "<password>"; // e.g. password = "password".            

            String authenticatorName = "BlackBerry Administration Service";
            String domain = null; // not needed

            return demonstrateBwsSetupAndAuthenticatedCall(bwsHostname, bwsPort, username, password,
                    domain, authenticatorName, credentialType);
        }

        /// <summary>
        /// Demonstrates Active Directory Authentication. Fields denoted by "<value>" must be manually set.
        /// </summary>
        /// 
        /// <returns>Returns true if authenticated successfully, false otherwise.</returns>
        private static bool demonstrateActiveDirectoryAuthentication(String bwsHostname, String bwsPort,
            CredentialType credentialType)
        {
            logMessage("Attempting Active Directory authentication");

            // The Active Directory Credentials to use
            String username = "<username>"; // e.g. username = "admin"
            String password = "<password>"; // e.g. password = "password"
            String authenticatorName = "Active Directory";
            String activeDirectoryDomain = null;
            // Only UEM and BES12 require domain for authentication
            if (serverType == ServerType.BDS || serverType == ServerType.BES12)
            {
                activeDirectoryDomain = "<domain>";// e.g. activeDirectoryDomain = "example.net"
            }

            return demonstrateBwsSetupAndAuthenticatedCall(bwsHostname, bwsPort, username, password,
                    activeDirectoryDomain, authenticatorName, credentialType);
        }

        /// <summary>
        /// Demonstrates LDAP Authentication. Fields denoted by "<value>" must be manually set.
        /// </summary>
        /// 
        /// <returns>Returns true if authenticated successfully, false otherwise.</returns>
        private static bool demonstrateLDAPAuthentication(String bwsHostname, String bwsPort,
            CredentialType credentialType)
        {
            logMessage("Attempting LDAP authentication");

            // The LDAP Credentials to use
            String username = "<username>"; // e.g. username = "admin"
            String password = "<password>"; // e.g. password = "password"
            String authenticatorName = "LDAP";
            String domain = null; // not needed

            return demonstrateBwsSetupAndAuthenticatedCall(bwsHostname, bwsPort, username, password,
                    domain, authenticatorName, credentialType);
        }

        /// <summary>
        /// Tests if the passed in settings successfully authenticate against BWS.
        /// </summary>
        /// 
        /// <returns>Returns true if authenticated successfully, false otherwise.</returns>        
        private static bool demonstrateBwsSetupAndAuthenticatedCall(String bwsHostname, String bwsPort,
            String username, String password, String domain, String authenticatorName,
            CredentialType credentialType)
        {
            bool returnCode = false;
            logMessage("Initializing web services...");
            if (setup(bwsHostname, bwsPort, username, password, authenticatorName, credentialType, domain))
            {
                /*
                 * It is anticipated that the first time through this method, _serverType will be unknown. 
                 * So getSystemInfo() will populate this value, which will be used in the subsequent 
                 * demonstrate calls if required.
                 */
                if (serverType == ServerType.Unknown)
                {
                    GetSystemInfo();
                }
                /*
                 * Demonstrate authenticated call to bwsService.echo() API.
                 */
                logMessage("Attempting authenticated BWS call to echo()...");
                if (echo())
                {
                    logMessage("Authenticated call succeeded!");
                    returnCode = true;
                }
                else
                {
                    logMessage("Authenticated call failed!");
                }
            }
            else
            {
                logMessage("Error: setup() failed");
            }
            return returnCode;
        }

        /// <summary>
        /// Initialize the BWS and BWSUtil services.
        /// </summary>
        /// 
        /// <returns>Returns true when the setup is successful, and false otherwise.</returns>
        private static bool setup(String hostname, String bwsPort, String username, String password,
            String authenticatorName, CredentialType credentialType, String domain)
        {
            const string METHOD_NAME = "setup()";
            logMessage("Entering {0}", METHOD_NAME);
            bool returnValue = false;

            REQUEST_METADATA.clientVersion = CLIENT_VERSION;
            REQUEST_METADATA.locale = LOCALE;
            REQUEST_METADATA.organizationUid = ORG_UID;

            logMessage("Initializing BWS web service stub");
            bwsService = new BWSService();
            logMessage("BWS web service stub initialized");
            logMessage("Initializing BWSUtil web service stub");
            bwsUtilService = new BWSUtilService();
            logMessage("BWSUtil web service stub initialized");
            // These are the URLs that point to the web services used for all calls.
            // e.g. with no port:
            // https://server01.example.net/enterprise/admin/ws
            // e.g. with port:
            // https://server01.example.net:18084/enterprise/admin/ws

            String port = "";

            if (bwsPort != null)
            {
                port = ":" + bwsPort;
            }

            bwsService.Url = "https://" + hostname + port + "/enterprise/admin/ws";
            bwsUtilService.Url = "https://" + hostname + port + "/enterprise/admin/util/ws";

            // Set the connection timeout to 60 seconds.
            bwsService.Timeout = 60000;
            bwsUtilService.Timeout = 60000;

            Authenticator authenticator = getAuthenticator(authenticatorName);
            if (authenticator != null)
            {
                string encodedUsername = getEncodedUserName(username, authenticator, credentialType, domain);
                if (!string.IsNullOrEmpty(encodedUsername))
                {
                    /* 
                     * Set the HTTP basic authentication on the BWS service.
                     * BWSUtilService is a utility web service that does not require
                     * authentication. 
                     */
                    bwsService.Credentials = new NetworkCredential(encodedUsername, password);

                    /* 
                     * Send an HTTP Authorization header with requests after authentication
                     * has taken place. 
                     */
                    bwsService.PreAuthenticate = true;
                    returnValue = true;
                }
                else
                {
                    logMessage("'encodedUsername' is null or empty");
                }
            }
            else
            {
                logMessage("'authenticator' is null");
            }

            logMessage("Exiting {0} with value \"{1}\"", METHOD_NAME, returnValue);
            return returnValue;
        }
    }
}