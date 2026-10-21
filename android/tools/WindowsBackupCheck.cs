using System;
using LexisDesk;
public static class WindowsBackupCheck {
  public static int Main(string[] args) {
    try {
      var data=JsonIO.Read<UserData>(args[0]);Store.Validate(data);
      if(data.Cards.Count!=2||data.History.Count!=3||data.CustomBooks.Count!=1||data.CustomWords.Count!=1||data.DailyTarget!=30||data.PageStyle!="luminous"||data.LuminousMotion||!data.ExcludedWords.Contains("might"))throw new Exception("Imported Android fields do not match.");
      if(data.Cards["state"].Due!="2026-10-21T04:10:00.0000000Z")throw new Exception("Review deadline changed.");
      var store=new Store(args[2]);store.Restore(args[0]);
      if(!store.Words.ContainsKey("custom:resilient")||store.Data.Cards["state"].Reviews!=2)throw new Exception("Full Windows Store restore failed.");
      JsonIO.Write(args[1],store.Data);
      Console.WriteLine("PASS: Windows v1.5.0 reads, validates and restores Android backup with exact due dates.");
      Console.WriteLine("PASS: Windows exports the migrated data back to JSON.");return 0;
    }catch(Exception error){Console.Error.WriteLine(error);return 1;}
  }
}
