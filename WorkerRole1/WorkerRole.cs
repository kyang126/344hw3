using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using System.Configuration;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System.IO;
using System.Text.RegularExpressions;
using ClassLibrary1;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        public override void Run()
        {
            Trace.TraceInformation("WorkerRole1 is running");

            try
            {

                while (true)
                {
                    getReference g = new getReference();
                    CloudQueue queue = g.commandQueue();
                    queue.CreateIfNotExists();
                    CloudQueueMessage retrievedMessage = queue.GetMessage();

                    if (retrievedMessage != null && retrievedMessage.AsString.Equals("start"))
                    {
                        queue.DeleteMessage(retrievedMessage);
                        crawlRobots();
                    }

                    else if (retrievedMessage != null && retrievedMessage.AsString.Equals("stop"))
                    {
                        break;
                    }
                    

                    Thread.Sleep(50);
                }
                
           
              this.RunAsync(this.cancellationTokenSource.Token).Wait();
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }


         public void crawlRobots()
        {
            List<String> noRobots = new List<String>();
            List<String> allXml = new List<String>();
            var wc = new WebClient();
            String robot = "http://bleacherreport.com/robots.txt";
            int count = 0;
            List<String> yesRobots = new List<String>();
            for (int i = 0; i < 2; i++)
            {
                using (var sourceStream = wc.OpenRead(robot))
                {
                    using (var reader = new StreamReader(sourceStream))
                    {
                        while (reader.EndOfStream == false)
                        {

                            string line = reader.ReadLine();
                            if (!line.Contains("User-Agent"))
                            {

                                if (line.Contains("Sitemap:"))
                                {
                                    String newUrl = line.Replace("Sitemap:", "").Trim();

                                    if (robot.Contains("bleacher") && newUrl.Contains("nba"))
                                    {
                                        yesRobots.Add(newUrl);
                                    }
                                    else if (robot.Contains("cnn"))
                                    {
                                        yesRobots.Add(newUrl);
                                    }
                                }
                                if (line.Contains("Disallow:"))
                                {
                                    String newUrl = line.Replace("Disallow:", "").Trim();

                                    if (robot.Contains("bleacher"))
                                    {
                                        newUrl = "http://bleacherreport.com" + newUrl;
                                    }
                                    else
                                    {
                                        newUrl = "http://cnn.com" + newUrl;
                                    }

                                    noRobots.Add(newUrl);
                                }
                            }
                        }
                    }
                }
                count++;
                if (robot.Contains("cnn"))
                {
                    allXml = getAllXml(yesRobots);
                }
                robot = "http://www.cnn.com/robots.txt";
            }
                
             crawlerUrls(allXml, noRobots);   
        }


         private List<String> getAllXml(List<String> o)
         {
             List<String> oldList = o;
             int count = 0;
             while (count < oldList.Count)
             {
                 WebClient web = new WebClient();
                 String html = web.DownloadString(oldList.ElementAt(count));
                 MatchCollection m1 = Regex.Matches(html, @"<loc>\s*(.+?)\s*</loc>", RegexOptions.Singleline);
                 String index = oldList.ElementAt(count);
                 foreach (Match m in m1)
                 {
                     String url = m.Groups[1].Value;
                     if (url.Contains("xml") && ((url.Contains("2015") || !url.Contains("-20"))))
                     {
                        oldList.Add(url);

                        oldList.Remove(index);
                     }
                 }
                 count++;
             }
             return oldList;
         }

        public void crawlerUrls(List<String> xmlList, List<String> noRobots)
        {
            List<String> urlList = new List<String>();
            for (int i = 0; i < xmlList.Count; i++)
            {
                String xml = xmlList.ElementAt(i);
                WebClient web = new WebClient();
                String html = web.DownloadString(xml);
                MatchCollection m1 = Regex.Matches(html, @"<loc>\s*(.+?)\s*</loc>", RegexOptions.Singleline);
                getReference g = new getReference();
                CloudQueue queue = g.getQueue();
                CloudQueue cmd = g.commandQueue();

               

                foreach (Match m in m1)
                {

                    CloudQueueMessage cmdMessage = cmd.GetMessage();

                    if (cmdMessage != null && cmdMessage.AsString.Equals("stop"))
                    {
                        cmd.DeleteMessage(cmdMessage);
                        return;
                    }

                    String url = m.Groups[1].Value;
                    urlList.Add(url);
                    CloudQueueMessage message = new CloudQueueMessage(url);
                    queue.AddMessage(message);
                }
              urlList = getAllUrls(urlList, noRobots);
            }
        }

        private List<String> getAllUrls(List<String> o, List<String> noCNN)
        {
            getReference g = new getReference();
            CloudQueue queue = g.getQueue();
            CloudTable table = g.getTable();
            List<String> oldList = o;

            int count = 0;
            queue.FetchAttributes();
            while(count < queue.ApproximateMessageCount.Value || count < 100)
            {
                WebClient web = new WebClient();
                String html = web.DownloadString(oldList.ElementAt(count));
                MatchCollection m1 = Regex.Matches(html, @"<title>\s*(.+?)\s*</title>", RegexOptions.Singleline);
                MatchCollection m2 = Regex.Matches(html, @"<a href=""\s*(.+?)\s*""", RegexOptions.Singleline);
                MatchCollection m3 = Regex.Matches(html, @"<meta content=""\s*(.+?)\s*"" itemprop=""dateCreated", RegexOptions.Singleline);
                String root = "";


                if (oldList.ElementAt(count).Contains("bleacherreport"))
                {
                    root = "http://bleacherreport.com";
                }
                else
                {
                    root = "http://cnn.com";
                }

        
                String title = "";
                if (m1 != null)
                {
                    Match mt = m1[0];
                    title = mt.Groups[1].Value;
                }

               
                String date = "";
                if (m3 != null && m3.Count > 0)
                {
                    Match md = m3[0];
                    date = md.Groups[1].Value;
                }
                CloudQueueMessage retrievedMessage = queue.GetMessage();
                if (retrievedMessage != null)
                {
                    crawledTable ct = new crawledTable("344 HW 3", retrievedMessage.AsString, title, date);
                    TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(ct);
                    table.Execute(insertOrReplaceOperation);
                    queue.DeleteMessage(retrievedMessage);
                    count++;
                }                
                
                foreach (Match m in m2)
                {
                    CloudQueue cmd = g.commandQueue();

                    CloudQueueMessage cmdMessage = cmd.GetMessage();
                    if (cmdMessage != null && cmdMessage.AsString.Equals("stop"))
                    {
                        cmd.DeleteMessage(cmdMessage);
                        return new List<String>();
                    }

                    String url = m.Groups[1].Value;
                    if (url.StartsWith("/"))
                    {
                        url = root + url;
                    }
                    
                    if (!oldList.Contains(url) && !noCNN.Contains(url) && (url.Contains(root)))
                    {
                        oldList.Add(url);
                        CloudQueueMessage message = new CloudQueueMessage(url);
                        queue.AddMessage(message);
                    }
                }                
            }

            return oldList;
        }
    
        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            Trace.TraceInformation("WorkerRole1 has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("WorkerRole1 is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("WorkerRole1 has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following with your own logic.
            while (!cancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Working");
                await Task.Delay(1000);
            }
        }
    }
}
