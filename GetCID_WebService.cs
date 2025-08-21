using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace HGM.Hotbird64.LicenseManager
{
    public partial class GetCID_WebService
    {
        private readonly byte[] MacKey = [
            254,  49, 152, 117, 251,  72, 132, 134,
            156, 243, 241, 206, 153, 168, 144, 100,
            171,  87,  31, 202,  71,   4,  80,  88,
            48,   36, 226,  20,  98, 135, 121, 160,
            0,     0,   0,   0,   0,   0,   0,   0,
            0,     0,   0,   0,   0,   0,   0,   0,
            0,     0,   0,   0,   0,   0,   0,   0,
            0,     0,   0,   0,   0,   0,   0,   0
        ];

        protected const string Action = "http://www.microsoft.com/BatchActivationService/BatchActivate";
        protected readonly Uri Uri = new("https://activation.sls.microsoft.com/BatchActivation/BatchActivation.asmx");
        protected readonly XNamespace SoapSchemaNs = "http://schemas.xmlsoap.org/soap/envelope/";
        protected readonly XNamespace XmlSchemaInstanceNs = "http://www.w3.org/2001/XMLSchema-instance";
        protected readonly XNamespace XmlSchemaNs = "http://www.w3.org/2001/XMLSchema";
        protected readonly XNamespace BatchActivationServiceNs = "http://www.microsoft.com/BatchActivationService";
        protected readonly XNamespace BatchActivationRequestNs = "http://www.microsoft.com/DRM/SL/BatchActivationRequest/1.0";
        protected readonly XNamespace BatchActivationResponseNs = "http://www.microsoft.com/DRM/SL/BatchActivationResponse/1.0";

        protected readonly string webRequestType = "POST";
        protected readonly string webAppType = "text/xml";
        protected readonly string webRequestEncoding = "text/xml; charset=\"utf-8\"";
        protected readonly string webHost = "activation.sls.microsoft.com";

        public bool CheckConnection()
        {
            bool isInternetGood = false;

            try
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(webHost);
                if (hostEntry.AddressList.Length > 0)
                {
                    using
                    var ping = new Ping();
                    var reply = ping.Send(hostEntry.AddressList[0]);
                    if (reply.Status == IPStatus.Success)
                    {
                        isInternetGood = true;
                    }
                }
            }
            catch
            {
                isInternetGood = false;
            }

            return isInternetGood;
        }

        public string CallWebService(string installationId, string extendedProductId)
        {
            XElement activationRequest = new(BatchActivationRequestNs + "ActivationRequest",
                new XElement(BatchActivationRequestNs + "VersionNumber", "2.0"),
                new XElement(BatchActivationRequestNs + "RequestType", 1),
                new XElement(BatchActivationRequestNs + "Requests",
                    new XElement(BatchActivationRequestNs + "Request",
                        new XElement(BatchActivationRequestNs + "PID", extendedProductId),
                        new XElement(BatchActivationRequestNs + "IID", installationId)
                    )
                )
            );

            byte[] bytes = Encoding.Unicode.GetBytes(activationRequest.ToString());
            string requestXml = Convert.ToBase64String(bytes);
            XDocument soapRequest = new();

            using (HMACSHA256 hMACSHA = new(MacKey))
            {
                string digest = Convert.ToBase64String(hMACSHA.ComputeHash(bytes));
                soapRequest = new XDocument(
                    new XDeclaration("1.0", "UTF-8", "no"),
                    new XElement(SoapSchemaNs + "Envelope",
                        new XAttribute(XNamespace.Xmlns + "soap", SoapSchemaNs),
                        new XAttribute(XNamespace.Xmlns + "xsi", XmlSchemaInstanceNs),
                        new XAttribute(XNamespace.Xmlns + "xsd", XmlSchemaNs),
                        new XElement(SoapSchemaNs + "Body",
                            new XElement(BatchActivationServiceNs + "BatchActivate",
                                new XElement(BatchActivationServiceNs + "request",
                                    new XElement(BatchActivationServiceNs + "Digest", digest),
                                    new XElement(BatchActivationServiceNs + "RequestXml", requestXml)
                                )
                            )
                        )
                    ));
            }

            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(Uri);
            webRequest.Accept = webAppType;
            webRequest.ContentType = webRequestEncoding;
            webRequest.Headers.Add("SOAPAction", Action);
            webRequest.Host = webHost;
            webRequest.Method = webRequestType;

            using Stream stream = webRequest.GetRequestStream();
            soapRequest.Save(stream);

            XDocument soapResponse = new();

            IAsyncResult asyncResult = webRequest.BeginGetResponse(null, null);
            asyncResult.AsyncWaitHandle.WaitOne();
            using (WebResponse webResponse = webRequest.EndGetResponse(asyncResult))
            using (StreamReader streamReader = new(webResponse.GetResponseStream()))
            {
                soapResponse = XDocument.Parse(streamReader.ReadToEnd());
            }

            if (soapResponse == null)
            {
                throw new NullReferenceException();
            }

            if (!soapResponse.Descendants(BatchActivationServiceNs + "ResponseXml").Any())
            {
                throw new InvalidOperationException("ResponseXml not found in the SOAP response.");
            }

            XDocument responseXml = XDocument.Parse(soapResponse.Descendants(BatchActivationServiceNs + "ResponseXml").First().Value);

            if (responseXml.Descendants(BatchActivationResponseNs + "ErrorCode").Any())
            {
                throw new InvalidOperationException(responseXml.Descendants(BatchActivationResponseNs + "ErrorCode").First().Value);
            }
            else
            {
                var cidElement = responseXml.Descendants(BatchActivationResponseNs + "CID").FirstOrDefault();
                return cidElement.Value;
            }
        }
    }
}