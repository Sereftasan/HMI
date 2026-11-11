using _MK2010.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace _MK2010.Bussiness
{
    public class ReadJobBussiness
    {
        public string FileName = "CurrentList.dbxc";

        public List<JobEntity> ParseFile(string filePath, bool isCurrent = false)
        {
            List<JobEntity> jobList = new List<JobEntity>();

            JobEntity job;

            foreach (var line in File.ReadLines(filePath))
            {
                // Boş, yorum veya başında boşluk olan satırları geç
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith(" "))
                    continue;

                // Sadece rakamlar ve ; karakteri içermeli (isteğe bağlı olarak boşluk da dahil edilebilir)
                if (!Regex.IsMatch(line, @"^[0-9;\s]*$"))
                    continue;

                // Geçerli satırsa işle
                job = new JobEntity();
                string[] entity = line.Split(';');

                // Hatalı veya eksik satır varsa atla
                if (entity.Length < 6)
                    continue;

                try
                {
                    job.OrderNo = entity[0];
                    job.TrolleyNo = entity[1];
                    job.TrolleyShelfNo = entity[2];
                    job.Width = Int32.Parse(entity[3]) / 10;
                    job.Height = Int32.Parse(entity[4]) / 10;
                    job.ProfileCode = entity[5];

                    job.CustomBarcode = (entity.Length >= 7 && entity[6] != null) ? entity[6] : "";

                    jobList.Add(job);
                }
                catch
                {
                    // Parse hatası olursa satırı atla
                    continue;
                }
            }


            if (!isCurrent)
            {
                File.Delete(filePath);
            }

            return jobList;
        }

        public List<JobEntity> ReadCurrentJobList()
        {
            if (!File.Exists(Path.Combine(Application.StartupPath, FileName).ToString()))
            {
                return null;
            }

            return ParseFile(Path.Combine(Application.StartupPath, FileName).ToString(), true);
        }

        public void WriteCurrentList(List<JobEntity> JobList)
        {
            string filePath = Path.Combine(Application.StartupPath, FileName);


            string directoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }


            if (File.Exists(filePath))
            {
                File.WriteAllText(filePath, string.Empty);
            }


            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                foreach (var job in JobList)
                {
                    string line = $"{job.OrderNo};{job.TrolleyNo};{job.TrolleyShelfNo};{job.Width};{job.Height};{job.ProfileCode};{job.CustomBarcode}";
                    writer.WriteLine(line);
                }
                writer.Close();
            }

        }

    }
}
