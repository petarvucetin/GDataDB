using System;
using System.Linq;
using System.Xml.Linq;

namespace GDataDB.Impl {
    public class Database : IDatabase {
        private readonly DatabaseClient client;
        private readonly string id;
        private readonly Uri worksheetFeed;

        public Database(DatabaseClient client, string id, Uri worksheetFeed) {
            this.client = client;
            this.id = id;
            this.worksheetFeed = worksheetFeed;
        }

        public ITable<T> CreateTable<T>(string name) where T: new() {
            var length = typeof(T).GetProperties().Length;
            var http = client.RequestFactory.CreateRequest();

            var request = new XDocument(
                new XElement(Utils.AtomNs + "entry",
                    new XElement(Utils.AtomNs + "title", name),
                    new XElement(Utils.SpreadsheetsNs + "rowCount", 1),
                    new XElement(Utils.SpreadsheetsNs + "colCount", length)
                    )
                );
            var response = http.UploadString(worksheetFeed.AbsoluteUri, request.ToString());
            var xmlResponse = XDocument.Parse(response);

            // i.e. https://spreadsheets.google.com/feeds/list/key/worksheetId/private/full
            var listFeedUri = DatabaseClient.ExtractEntryContent(new[] {xmlResponse.Root});

            // i.e. https://spreadsheets.google.com/feeds/worksheets/key/private/full/worksheetId/version
            var editUri = xmlResponse.Root
                .Elements(Utils.AtomNs + "link")
                .Where(e => e.Attribute("rel").Value == "edit")
                .Select(e => new Uri(e.Attribute("href").Value))
                .FirstOrDefault();

            // TODO write headers

            return new Table<T>(client, listFeedUri: listFeedUri, worksheetUri: editUri);

        }

        public ITable<T> GetTable<T>(string name) where T: new() {
            var uriBuilder = new UriBuilder(worksheetFeed) {
                Query = "title-exact=true&title=" + Utils.UrlEncode(name),
            };
            var http = client.RequestFactory.CreateRequest();
            var rawResponse = http.DownloadString(uriBuilder.Uri);
            var xmlResponse = XDocument.Parse(rawResponse);
            var feedUri = DatabaseClient.ExtractEntryContent(xmlResponse);

            if (feedUri == null)
                return null;

            var editUri = xmlResponse.Root
                .Elements(Utils.AtomNs + "entry")
                .SelectMany(e => e.Elements(Utils.AtomNs + "link"))
                .Where(e => e.Attribute("rel").Value == "edit")
                .Select(e => new Uri(e.Attribute("href").Value))
                .FirstOrDefault();

            return new Table<T>(client, listFeedUri: feedUri, worksheetUri: editUri);

            // ??? https://spreadsheets.google.com/feeds/tNXTXMh83yMWLVJfEgOWTvQ/tables
        }

        public void Delete() {
            var http = client.RequestFactory.CreateRequest();
            http.UploadString(new Uri("https://www.googleapis.com/drive/v2/files/" + id), method: "DELETE", data: "");
        }
    }
}