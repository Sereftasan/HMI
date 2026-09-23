using System;
using System.IO;
using System.Linq;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

class P {
    static int Main(string[] a){
        switch (a[0]) {
            case "first":   // a brand-new PC: nothing stored yet
                var n = UserStore.GetAllUsers().Count();
                Console.WriteLine("  first run: {0} default accounts, store created: {1}", n,
                    File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Users.dat")));
                return n == 3 ? 0 : 1;
            case "edit":    // the engineer sets the machine up
                UserStore.AddOrUpdateUser("tester", "secret", SecurityLevel.Maintenance);
                UserStore.RemoveUser("operator");
                UnitManager.SetActive(UnitType.Length, "inch");
                Console.WriteLine("  edited: added 'tester', removed 'operator', length unit -> inch");
                return 0;
            case "restart": // the program is started again from the Debug folder
                var users = UserStore.GetAllUsers().Select(u => u.Username).ToList();
                bool kept = users.Contains("tester") && !users.Contains("operator");
                bool pw = UserStore.TryValidate("tester", "secret", out var acc);
                var unit = UnitManager.GetActive(UnitType.Length).Name;
                Console.WriteLine("  after restart: users = {0}", string.Join(", ", users));
                Console.WriteLine("  {0}  accounts kept (tester there, operator still gone)", kept ? "PASS" : "FAIL");
                Console.WriteLine("  {0}  tester's password still works", pw ? "PASS" : "FAIL");
                Console.WriteLine("  {0}  length unit still inch   [{1}]", unit == "inch" ? "PASS" : "FAIL", unit);
                return kept && pw && unit == "inch" ? 0 : 1;
        }
        return 2;
    }
}

