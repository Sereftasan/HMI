using _MD3030.Bussiness;
using _MD3030.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _MD3030.Business
{
    public static class JobControllerBussiness
    {
        public static List<JobEntity> JobList = new List<JobEntity>();
        static ReadJobBussiness ReadJobs =  new ReadJobBussiness();

        

        public static List<JobEntity> GetJobList()
        {
            if(JobList == null || JobList.Count <=0)
            {
               InitCurrentJobList();
            }

            return JobList;
        }

        private static void InitCurrentJobList()
        {
            if(ReadJobs.ReadCurrentJobList()!= null)
            {
                JobList = ReadJobs.ReadCurrentJobList();
            }
            
        }

        public static void AddJobList(List<JobEntity> joblist)
        {
            foreach(JobEntity job in joblist)
            {
                JobList.Add(job);
            }
            ReadJobs.WriteCurrentList(JobList);
        }

        public static void AddJob(JobEntity job)
        {
            JobList.Add(job);
            ReadJobs.WriteCurrentList(JobList);
        }

        public static void RemoveJob(JobEntity job)
        {
            if (!JobList.Contains(job))
            {
                return;
            }

            JobList.Remove(job);
            ReadJobs.WriteCurrentList(JobList);
        }

        public static void MoveJobToTop(JobEntity job)
        {
            if (!JobList.Contains(job))
            {
                return;
            }

            JobList.Remove(job);
            JobList.Insert(0, job);
            ReadJobs.WriteCurrentList(JobList);
        }

        public static void CleanJobList()
        {
            JobList.Clear();
            ReadJobs.WriteCurrentList(JobList);
        }
    }
}
