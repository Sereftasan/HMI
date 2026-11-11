using _MK2010.Business;
using _MK2010.Presentation.Home.Preparations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _MK2010.Bussiness
{
    class FileWatcherBussiness
    {
        public void WatchFolder()
        {
            string sourceDirectory = @"C:\KabanTransferMK2010";
            string tempDirectory = Path.Combine(Path.GetTempPath(), "tempFiles");

            // Ensure the temp directory exists
            if (!Directory.Exists(tempDirectory))
            {
                Directory.CreateDirectory(tempDirectory);
            }

            ProcessExistingFiles(sourceDirectory, tempDirectory);

            // Initialize the FileSystemWatcher
            FileSystemWatcher watcher = new FileSystemWatcher
            {
                Path = sourceDirectory,
                Filter = "*.csv",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };

            // Event handlers
            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;

            // Begin watching
            watcher.EnableRaisingEvents = true;

            // Keep the application running

            void OnCreated(object sender, FileSystemEventArgs e)
            {
                ProcessFile(e.FullPath);
            }

            void OnChanged(object sender, FileSystemEventArgs e)
            {
                ProcessFile(e.FullPath);
            }

            void ProcessFile(string filePath)
            {
                try
                {
                    string fileName = Path.GetFileName(filePath);
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                    string tempFilePath = Path.Combine(tempDirectory, fileName);

                    if (File.Exists(filePath))
                    {
                        File.Move(filePath, tempFilePath);

                        ReadJobBussiness readJob = new ReadJobBussiness();


                        var jobs = readJob.ParseFile(tempFilePath);
                        JobControllerBussiness.AddJobList(jobs);

                        if (ucJobNew.Instance().IsUserBarcodeActive())
                        {
                            ucJobListWithUserBarcode jobListWithUserBarcode = ucJobListWithUserBarcode.Instance();
                            jobListWithUserBarcode.FillGridSafe();
                        }
                        else
                        {

                            ucJobList jobList = ucJobList.Instance();
                            jobList.FillGridSafe();
                        }

                        


                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file: {ex.Message}");
                }
            }

            void ProcessExistingFiles(string sourceDir, string tempDir)
            {
                var csvFiles = Directory.GetFiles(sourceDir, "*.csv");
                
                foreach (var filePath in csvFiles)
                {
                    ProcessFile(filePath); // Process each file just like when a new file is created
                }
            }

        }
    }
}
