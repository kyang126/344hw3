using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
{
    public class crawledTable : TableEntity
    {
        public crawledTable(string value, string url, string title)
        {
            this.PartitionKey = value;
            this.RowKey = Guid.NewGuid().ToString();
            this.url = url;
            this.title = title;
        }

        public crawledTable() { }

        public string url { get; set; }

        public string title { get; set; }


    }
}