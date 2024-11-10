using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace HGM.Hotbird64.LicenseManager
{
    public partial class ActivationHelper
    {
        private static readonly byte[] MacKey = [
            254,  49, 152, 117, 251,  72, 132, 134,
            156, 243, 241, 206, 153, 168, 144, 100,
            171,  87,  31, 202,  71,   4,  80,  88,
            48,   36, 226,  20,  98, 135, 121, 160,
            0,     0,   0,   0,   0,   0,   0,   0,
            0,     0,   0,   0,   0,   0,   0,   0,
            0,     0,   0,   0,   0,   0,   0,   0,
            0,     0,   0,   0,   0,   0,   0,   0
        ];
        private const string Action = "http://www.microsoft.com/BatchActivationService/BatchActivate";
        private static readonly Uri Uri = new("https://activation.sls.microsoft.com/BatchActivation/BatchActivation.asmx");
        private static readonly XNamespace SoapSchemaNs = "http://schemas.xmlsoap.org/soap/envelope/";
        private static readonly XNamespace XmlSchemaInstanceNs = "http://www.w3.org/2001/XMLSchema-instance";
        private static readonly XNamespace XmlSchemaNs = "http://www.w3.org/2001/XMLSchema";
        private static readonly XNamespace BatchActivationServiceNs = "http://www.microsoft.com/BatchActivationService";
        private static readonly XNamespace BatchActivationRequestNs = "http://www.microsoft.com/DRM/SL/BatchActivationRequest/1.0";
        private static readonly XNamespace BatchActivationResponseNs = "http://www.microsoft.com/DRM/SL/BatchActivationResponse/1.0";

        public static string CallWebService(string installationId, string extendedProductId)
        {
            XDocument soapRequest = CreateSoapRequest(installationId, extendedProductId);
            HttpWebRequest webRequest = CreateWebRequest(soapRequest);
            XDocument soapResponse = new();

            try
            {
                IAsyncResult asyncResult = webRequest.BeginGetResponse(null, null);
                asyncResult.AsyncWaitHandle.WaitOne();
                using (WebResponse webResponse = webRequest.EndGetResponse(asyncResult))
                using (StreamReader streamReader = new(webResponse.GetResponseStream()))
                {
                    soapResponse = XDocument.Parse(streamReader.ReadToEnd());
                }
                return ParseSoapResponse(soapResponse);
            }
            catch
            {
                throw;
            }
        }

        private static XDocument CreateSoapRequest(string installationId, string extendedProductId)
        {
            int requestType = 1;
            XElement activationRequest = new(BatchActivationRequestNs + "ActivationRequest",
                new XElement(BatchActivationRequestNs + "VersionNumber", "2.0"),
                new XElement(BatchActivationRequestNs + "RequestType", requestType),
                new XElement(BatchActivationRequestNs + "Requests",
                    new XElement(BatchActivationRequestNs + "Request",
                        new XElement(BatchActivationRequestNs + "PID", extendedProductId),
                        requestType == 1 ? new XElement(BatchActivationRequestNs + "IID", installationId) : null)
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
            return soapRequest;
        }

        private static HttpWebRequest CreateWebRequest(XDocument soapRequest)
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(Uri);
            webRequest.Accept = "text/xml";
            webRequest.ContentType = "text/xml; charset=\"utf-8\"";
            webRequest.Headers.Add("SOAPAction", Action);
            webRequest.Host = "activation.sls.microsoft.com";
            webRequest.Method = "POST";
            try
            {
                using (Stream stream = webRequest.GetRequestStream()) { soapRequest.Save(stream); }
                return webRequest;
            }
            catch
            {
                throw;
            }
        }

        private static string ParseSoapResponse(XDocument soapResponse)
        {
            if (soapResponse == null)
            {
                return "Error: 0x194";
            }

            if (!soapResponse.Descendants(BatchActivationServiceNs + "ResponseXml").Any())
            {
                return "Error: 0x19D";
            }

            try
            {
                XDocument responseXml = XDocument.Parse(soapResponse.Descendants(BatchActivationServiceNs + "ResponseXml").First().Value);

                if (responseXml.Descendants(BatchActivationResponseNs + "ErrorCode").Any())
                {
                    string errorCode = responseXml.Descendants(BatchActivationResponseNs + "ErrorCode").First().Value;
                    return $"Error: {errorCode}";
                }
                else if (responseXml.Descendants(BatchActivationResponseNs + "ResponseType").Any())
                {
                    string responseType = responseXml.Descendants(BatchActivationResponseNs + "ResponseType").First().Value;
                    switch (responseType)
                    {
                        case "1":
                            string confirmationId = responseXml.Descendants(BatchActivationResponseNs + "CID").First().Value;
                            return confirmationId;
                        default:
                            return "Error: 0x2F78";
                    }

                }
                else
                {
                    return "Error: 0x2F78";
                }

            }
            catch
            {
                throw;
            }
        }
    }
}