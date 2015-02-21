using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Services;
using ClassLibrary1;
using System.Text;
using HtmlAgilityPack;

namespace WebRole1
{
    /// <summary>
    /// Summary description for index
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class index : System.Web.Services.WebService
    {
        private List<String> htmlFiles = new List<String>();


        [WebMethod]
        public void startCrawl()
        {
            getReference g = new getReference();
            CloudQueue queue = g.commandQueue();
            queue.CreateIfNotExists();
            CloudQueueMessage message = new CloudQueueMessage("start");
            queue.AddMessage(message);
        }

        [WebMethod]
        public void stopCrawl()
        {
            getReference g = new getReference();
            CloudQueue queue = g.commandQueue();
            queue.CreateIfNotExists();
            CloudQueueMessage message = new CloudQueueMessage("stop");
            queue.AddMessage(message);

            CloudQueue storage = g.getQueue();
            storage.Clear();
            CloudTable table = g.getTable();
            table.Delete();

        }

        [WebMethod]
        public String getQueueCount()
        {
            getReference g = new getReference();
            CloudQueue queue = g.getQueue();
            queue.CreateIfNotExists();
            queue.FetchAttributes();
            int approximateMessagesCount = queue.ApproximateMessageCount.Value;
            return "" + approximateMessagesCount;
        }

        [WebMethod]
        public String getcmdCount()
        {
            getReference g = new getReference();
            CloudQueue queue = g.commandQueue();
            queue.CreateIfNotExists();
            queue.FetchAttributes();
            int approximateMessagesCount = queue.ApproximateMessageCount.Value;
            return "" + approximateMessagesCount;
        }

        [WebMethod]
        public void clearCmd()
        {
            getReference g = new getReference();
            CloudQueue queue = g.commandQueue();
            queue.Clear();
        }

        [WebMethod]
        public void clearQ()
        {
            getReference g = new getReference();
            CloudQueue queue = g.getQueue();

            queue.Clear();
        }

        [WebMethod]
        public void deleteTable()
        {
            getReference g = new getReference();
            CloudTable table = g.getTable();
            table.Delete();
        }

        [WebMethod]
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

        [WebMethod]
        public void crawlerUrls(List<String> xmlList, List<String> noRobots)
        {
            HashSet<String> urlList = new HashSet<string>();
            getReference g = new getReference();
            CloudQueue queue = g.getQueue();
            CloudTable table = g.getTable();
            CloudQueue cmd = g.commandQueue();
            for (int i = 0; i < xmlList.Count; i++)
            {
                String xml = xmlList.ElementAt(i);
                WebClient web = new WebClient();
                String html = web.DownloadString(xml);
                MatchCollection m1 = Regex.Matches(html, @"<loc>\s*(.+?)\s*</loc>", RegexOptions.Singleline);

                CloudQueueMessage cmdMessage = cmd.GetMessage();

                if (cmdMessage != null && cmdMessage.AsString.Equals("stop"))
                {
                    cmd.DeleteMessage(cmdMessage);
                    return;
                }

                foreach (Match m in m1)
                {
                    String url = m.Groups[1].Value;
                    urlList.Add(url);
                    CloudQueueMessage message = new CloudQueueMessage(url);
                    queue.AddMessage(message);

                }
                //urlList = getAllUrls(urlList, noRobots);

                  queue.FetchAttributes();
                var limitCount = queue.ApproximateMessageCount.Value;
                int count = 0;
                  while (0 < limitCount)
                  {


                      String html1 = "";
                      CloudQueueMessage retrievedMessage = queue.GetMessage();

                      if (retrievedMessage != null)
                      {

                          using (WebClient webClient = new WebClient())
                          {
                              webClient.Encoding = Encoding.UTF8;
                              html1 = webClient.DownloadString(retrievedMessage.AsString);
                          }
                          MatchCollection titles = Regex.Matches(html1, @"<title>\s*(.+?)\s*</title>", RegexOptions.Singleline);
                          MatchCollection links = Regex.Matches(html1, @"<a href=""\s*(.+?)\s*""", RegexOptions.Singleline);
                          MatchCollection dates = Regex.Matches(html1, @"<meta content=""\s*(.+?)\s*"" itemprop=""dateCreated", RegexOptions.Singleline);



                          String root = "";

                          if (retrievedMessage.AsString.Contains("bleacher"))
                          {
                              root = "bleacherreport.com";
                          }
                          else if (retrievedMessage.AsString.Contains("cnn"))
                          {
                              root = "cnn.com";
                          }
                          int test = links.Count;
                          foreach (Match m in links)
                          {
                              count++;
                              String url = m.Groups[1].Value;
                              if (url.StartsWith("//"))
                              {
                                  url = "http:" + url;
                              }
                              else if (url.StartsWith("/"))
                              {
                                  url = "http://" + root + url;
                              }
                              if (!urlList.Contains(url) && !noRobots.Contains(url) && (url.Contains(root + "/")))
                              {
                                  urlList.Add(url);
                                  CloudQueueMessage message = new CloudQueueMessage(url);
                                  queue.AddMessage(message);
                              }
                          }
                       



                          String title = "";
                          if (titles != null && titles.Count > 0)
                          {
                              Match mt = titles[0];
                              title = mt.Groups[1].Value;
                          }

                          String date = "";
                          if (dates != null && dates.Count > 0)
                          {
                              Match md = dates[0];
                              date = md.Groups[1].Value;
                          }


                          queue.DeleteMessage(retrievedMessage);
                          crawledTable ct = new crawledTable("new inside test", retrievedMessage.AsString, title, date);
                          TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(ct);
                          table.Execute(insertOrReplaceOperation);

                          queue.FetchAttributes();
                          limitCount = queue.ApproximateMessageCount.Value;






                       


                      }


                  }
            }
        }

        private HashSet<String> getAllUrls(HashSet<String> o, List<String> disallowed)
        {
            getReference g = new getReference();
            CloudQueue queue = g.getQueue();
            CloudTable table = g.getTable();
            CloudQueue cmd = g.commandQueue();
            HashSet<String> oldList = o;

            int count = 0;
            queue.FetchAttributes();
            while (0 < queue.ApproximateMessageCount.Value || count < 100)
            {
                WebClient web = new WebClient();
                String html = web.DownloadString(oldList.ElementAt(count));
                MatchCollection m1 = Regex.Matches(html, @"<title>\s*(.+?)\s*</title>", RegexOptions.Singleline);
                MatchCollection m2 = Regex.Matches(html, @"<a href=""\s*(.+?)\s*""", RegexOptions.Singleline);
                MatchCollection m3 = Regex.Matches(html, @"<meta content=""\s*(.+?)\s*"" itemprop=""dateCreated", RegexOptions.Singleline);
                String root = "";

                if (oldList.ElementAt(count).Contains("bleacherreport"))
                {
                    root = "bleacherreport.com";
                }
                else
                {
                    root = "cnn.com";
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
                    String url = m.Groups[1].Value;
                    if (url.StartsWith("/"))
                    {
                        url = root + url;
                    }
                    if (!oldList.Contains(url) && !disallowed.Contains(url) && (url.Contains(root + "/")))
                    {
                        oldList.Add(url);
                        CloudQueueMessage message = new CloudQueueMessage(url);
                        queue.AddMessage(message);
                    }
                }



            }

            return oldList;
        }

    }
}
