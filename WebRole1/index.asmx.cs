using HtmlAgilityPack;
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

namespace WebRole1
{
    /// <summary>
    /// Summary description for index
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService]
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
        }

        [WebMethod]
        public int getQueueCount()
        {
            getReference g = new getReference();
            CloudQueue queue = g.getQueue();
            queue.CreateIfNotExists();
            queue.FetchAttributes();
            int approximateMessagesCount = queue.ApproximateMessageCount.Value;
            return approximateMessagesCount;

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
        public List<String> crawlRobots()
        {
            
            List<String> noRobots = new List<String>();
            List<String> allXml = new List<String>();
            var wc = new WebClient();
            String robot = "http://www.cnn.com/robots.txt"; 
            int count = 0;
            List<String> yesRobots = new List<String>();

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
                
                count++;             
                if (robot.Contains("cnn"))
                {
                    allXml = getAllXml(yesRobots);
                }
             
            }
                

            if (robot.Contains("bleacher")){
           // return crawlerUrls("http://bleacherreport.com/sitemap/nba.xml", noRobots);
            }
            else
            {
             return crawlerUrls(allXml, noRobots);
            }

           return allXml;

        }


        [WebMethod]
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

        public List<String> crawlerUrls(List<String> xmlList, List<String> noRobots)
        {
            List<String> urlList = new List<String>();
            for (int i = 0; i < xmlList.Count; i++)
            {
                List<String> oldList = new List<String>();
                String xml = xmlList.ElementAt(i);
                WebClient web = new WebClient();
                String html = web.DownloadString(xml);
                MatchCollection m1 = Regex.Matches(html, @"<loc>\s*(.+?)\s*</loc>", RegexOptions.Singleline);
               // urlList.Add(m1.ToString());
                //String url = m1[1].Value;
                Match m3 = m1[0];
                String url = m3.Groups[1].Value;
                urlList.Add(url);
               /* foreach (Match m in m1)
                {
                    String url = m.Groups[1].Value;
                    urlList.Add(url);
                    oldList.Add(url);
                } */
               // urlList = getAllUrls(oldList, noRobots, urlList);
            }
            urlList.Add(""+urlList.Count);
            return urlList;
        }

        private List<String> getAllUrls(List<String> o, List<String> noCNN, List<String> urlList)
        {
            List<String> oldList = o;

          
               for (int i = 0; i < oldList.Count; i++)
                {
                    try
                    {
                        WebClient web = new WebClient();
                        String html = web.DownloadString(oldList.ElementAt(i));
                        MatchCollection m2 = Regex.Matches(html, @"<a href=""\s*(.+?)\s*""", RegexOptions.Singleline);
                        String root = "http://cnn.com";
                        foreach (Match m in m2)
                        {
                            String orig = oldList.ElementAt(i);
                            String url = m.Groups[1].Value;
                            if (url.StartsWith("/"))
                            {
                                url = root + url;
                            }

                            if (!oldList.Contains(url) && !noCNN.Contains(url) && (url.Contains(root)))
                            {
                                urlList.Add(url);
                            }
                        }
                    }
                    catch (WebException ex)
                    {

                    }

       
                }
            return urlList;
        }

    }
}
