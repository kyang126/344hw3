using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure.Storage.Table;


namespace ClassLibrary1
{
    public class crawledTable : TableEntity
    {

        public crawledTable(string value, string url, string title, string date)
        {
            this.PartitionKey = value;
            this.RowKey = Guid.NewGuid().ToString();
            this.url = url;
            this.title = title;
            this.date = date;
        }

        public crawledTable() { }

        public string url { get; set; }

        public string title { get; set; }

        public string date { get; set; }

    }
}
