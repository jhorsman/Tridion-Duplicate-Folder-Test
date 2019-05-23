using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Threading;
using Tridion.ContentManager.CoreService.Client;

namespace CoreServiceFolderIssue
{
    class Tester
    {
        private string _folderPath;
        private List<string> _folders = new List<string>();

        static void Main()
        {
            Tester t = new Tester();
            t.DoTest();
        }

        public void DoTest()
        {
            // Pick a random folder name
            string folderName = "test " + (new Random()).Next(1, 999);
            _folderPath = string.Format(@"/webdav/{0}/{1}/{2}",
                ConfigurationManager.AppSettings["publicationName"], ConfigurationManager.AppSettings["basePath"], folderName);
            Console.WriteLine($"folder webDav path: '{_folderPath}'");
            Console.WriteLine("");

            // Start threads to create several folders simultaneously
            Thread thread1 = new Thread(() => CreateFolder("1"));
            Thread thread2 = new Thread(() => CreateFolder("2"));
            thread1.Start();
            //thread2.Start();

            // wait until all threads are done
            while (thread1.IsAlive || thread2.IsAlive)
            {
            }

            //Analyze the test results
            AnalyzeResults();

            Console.WriteLine("");
            Console.WriteLine("Press <any> key to continue");
            Console.ReadKey();
        }

        public void CreateFolder(string threadId)
        {
            try
            {
                Console.WriteLine($"{threadId} - Start thread to create folder '{_folderPath}'");

                CoreServiceClient client = new CoreServiceClient(ConfigurationManager.AppSettings["endpointName"], ConfigurationManager.AppSettings["endpointURI"]);
                client.ClientCredentials.Windows.ClientCredential = new NetworkCredential(ConfigurationManager.AppSettings["username"], ConfigurationManager.AppSettings["password"]);

                Console.WriteLine($"{threadId} - trying to connect to core service at {ConfigurationManager.AppSettings["endpointURI"]}");
                string apiVersion = client.GetApiVersion();
                Console.WriteLine($"{threadId} - API version: {apiVersion}");

                int lastSlashIdx = _folderPath.LastIndexOf("/");
                string newFolderPath = _folderPath.Substring(lastSlashIdx + 1);
                string parentFolder = _folderPath.Substring(0, lastSlashIdx);

                if (!client.IsExistingObject(parentFolder))
                {
                    Console.WriteLine($"{threadId} - ERROR base path does not exist '{parentFolder}'");
                    return;
                }

                FolderData parentFolderData = (FolderData)client.Read(parentFolder, new ReadOptions());        

                if (client.IsExistingObject(_folderPath))
                {
                    FolderData folderData = (FolderData) client.Read(_folderPath, new ReadOptions());
                    Console.WriteLine($"{threadId} - OK folder already exists {folderData.Id} '{_folderPath}'");
                }
                else
                {
                    FolderData newFolderData = (FolderData)client.GetDefaultData(ItemType.Folder, parentFolderData.Id, new ReadOptions());
                    newFolderData.Title = newFolderPath;
                    FolderData folderData = (FolderData)client.Save(newFolderData, new ReadOptions());
                    _folders.Add(folderData.Id);
                    Console.WriteLine($"{threadId} - Created new content folder {folderData.Id} '{newFolderPath}' under folder '{parentFolderData.Title}'");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"{threadId} - ERROR exception: '{ex.Message}'");
            }
        }

        private void AnalyzeResults()
        {
            // analyze the test results
            Console.WriteLine("");
            if (_folders.Count == 0)
            {
                // Failed test. Apparently the folder existed before starting the test.
                Console.WriteLine($"ERROR - unexpected: {_folders.Count} folders were created");
            }
            else if (_folders.Count == 1)
            {
                // OK test. The bug did not apprear
                Console.WriteLine($"OK - {_folders.Count} folders were created"); // one folder was created which is good. The bug is not reproduced
            }
            else if (_folders.Count > 1)
            {
                // Expected test result
                Console.WriteLine($"ERROR {_folders.Count} folders were created with the same folder name: '{_folderPath}'");

                CoreServiceClient client = new CoreServiceClient(ConfigurationManager.AppSettings["endpointName"],
                    ConfigurationManager.AppSettings["endpointURI"]);
                client.ClientCredentials.Windows.ClientCredential = new NetworkCredential(
                    ConfigurationManager.AppSettings["username"], ConfigurationManager.AppSettings["password"]);


                // Proving that IsExistingObject operation returns an exception when the target folder name is not unique
                Console.WriteLine("");
                try
                {
                    bool isExistingObject = client.IsExistingObject(_folderPath);
                    Console.WriteLine($"OK IsExistingObject '{_folderPath}' returned '{isExistingObject.ToString()}'");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR: IsExistingObject '{_folderPath}' returned exception '{e.Message}'");
                }


                // Proving that Read operation returns an exception when the target folder name is not unique
                Console.WriteLine("");
                try
                {
                    FolderData folderData = (FolderData)client.Read(_folderPath, new ReadOptions());
                    Console.WriteLine($"OK Read '{_folderPath}' returned FolderData for '{folderData.Id}'");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR: Read '{_folderPath}' returned exception '{e.Message}'");
                }
            }
        }
    }
}
