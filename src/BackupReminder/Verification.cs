using System.Text.Json;

namespace BackupReminder;
internal static class Verification
{
    internal static int Run(string root)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Directory.CreateDirectory(root);
        string report = Path.Combine(root,"verification.txt");
        var results = new List<string>();
        try
        {
            void Check(bool condition,string message) { if(!condition) throw new InvalidDataException(message); results.Add("PASS: "+message); }
            AssetVerification.Run(root,Check);
            NamedAssetVerification.Run(root,Check);
            SoundVerification.Run(root,Check);
            PickerVerification.Run(root,Check);
            string editDirectory=Path.Combine(root,"edit-sessions");
            using(var editWindow=new MainWindow(new Profile(editDirectory),Options.Parse(["--data-directory",editDirectory,"--self-test",root])))editWindow.VerifyEditSessions(Check);
            string wave=Path.Combine(root,"test sound.wav");
            using(var output=new BinaryWriter(File.Create(wave)))
            {
                output.Write("RIFF"u8);output.Write(38);output.Write("WAVEfmt "u8);output.Write(16);output.Write((short)1);output.Write((short)1);
                output.Write(8000);output.Write(8000);output.Write((short)1);output.Write((short)8);output.Write("data"u8);output.Write(2);output.Write(new byte[]{128,128});
            }
            int waves=0,defaults=0;
            Check(ReminderSound.Play(wave,_=>{waves++;return true;},()=>defaults++)&&waves==1&&defaults==0,"Valid custom WAV routes to playback without a Windows fallback");
            Check(!ReminderSound.Play(wave,_=>false,()=>defaults++)&&defaults==1,"Failed custom playback falls back to Windows once");
            Check(!ReminderSound.Play(wave,_=>throw new IOException(),()=>defaults++)&&defaults==2,"Custom playback exceptions fall back without interrupting reminders");
            File.Delete(wave);Check(!ReminderSound.Play(wave,_=>{waves++;return true;},()=>defaults++)&&waves==1&&defaults==3,"A moved or deleted custom WAV uses the Windows sound");
            File.WriteAllText(wave,"invalid wave");Check(!ReminderSound.Play(wave,_=>{waves++;return true;},()=>defaults++)&&waves==1&&defaults==4,"An invalid WAV uses the Windows sound");
            Check(!ReminderSound.Play(null,_=>true,()=>defaults++)&&defaults==5,"Default sound remains Windows sound");
            Check(!ReminderSound.Play(wave,_=>false,()=>throw new IOException()),"Audio failure never prevents opening a reminder");
            string defaultDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Backup Reminder");
            Check(WindowsReminderScheduler.FriendlyName("ExampleUser",defaultDirectory,Path.Combine(defaultDirectory,"settings.json"))=="Backup Reminder - ExampleUser","Default task name is a readable Windows username");
            var settings=ChecklistSettings.Parse(Profile.Resource("starter.json"));
            Check(settings.Items.Count==5 && settings.Items.Count(x=>x.Group is null)==2 && settings.Groups.Count==1,"Generic starter has two standalone tasks and three grouped jobs");
            foreach(var test in new[]{"duplicate","empty","orphan","version","duplicate-group"})
            {
                var invalid=JsonSerializer.Deserialize<ChecklistSettings>(Profile.Resource("starter.json"),Profile.Json)!;
                switch(test)
                {
                    case "duplicate":invalid.Items[1].Id=invalid.Items[0].Id;break;
                    case "empty":invalid.Items.Clear();break;
                    case "orphan":invalid.Items[2].Group="missing";break;
                    case "version":invalid.Version=999;break;
                    case "duplicate-group":invalid.Groups.Add(invalid.Groups[0]);break;
                }
                bool rejected=false;try{ChecklistSettings.Parse(JsonSerializer.Serialize(invalid,Profile.Json));}catch(InvalidDataException){rejected=true;}
                Check(rejected,"Reject invalid settings: "+test);
            }
            for(int year=2027;year<=2028;year++)for(int day=28;day<=31;day++)for(int month=1;month<=12;month++)
            {
                var parts=WindowsReminderScheduler.MonthlyParts(day).Where(x=>(x.Months&(1<<(month-1)))!=0).ToArray();
                Check(parts.Length==1&&(parts[0].Last?DateTime.DaysInMonth(year,month):parts[0].Day)==Math.Min(day,DateTime.DaysInMonth(year,month)),"Month-end schedule "+year+"/"+month+" day "+day);
            }
            string directory=Path.Combine(root,"roundtrip-"+Guid.NewGuid().ToString("N"));
            var profile=new Profile(directory);
            var first=File.ReadAllBytes(Path.Combine(directory,"settings.json"));
            profile.Save(["documents"],null);
            var reopened=new Profile(directory);
            Check(reopened.State.Checked.SequenceEqual(new[]{"documents"}) && reopened.State.LastCompleted is null,"Partial progress survives reopen");
            string completed="2026-10-17T12:00:00.0000000-05:00";reopened.Save([],completed);
            var finished=new Profile(directory);
            Check(finished.State.Checked.Count==0 && finished.State.LastCompleted==completed,"Completion clears checks and retains timestamp");
            Check(first.SequenceEqual(File.ReadAllBytes(Path.Combine(directory,"settings.json"))),"Saving progress preserves configuration");
            var savedBytes=File.ReadAllBytes(finished.StatePath);
            bool saveRejected=false;
            using(var locked=new FileStream(finished.StatePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {try{finished.Save(["documents"],null);}catch(Exception error) when(error is IOException or UnauthorizedAccessException){saveRejected=true;}}
            Check(saveRejected && finished.State.LastCompleted==completed && savedBytes.SequenceEqual(File.ReadAllBytes(finished.StatePath)),"Failed replacement preserves saved progress and in-memory completion");
            string previewDir=Path.Combine(root,"preview-does-not-write-"+Guid.NewGuid().ToString("N"));
            _=new Profile(previewDir,preview:true);
            Check(!Directory.Exists(previewDir),"Preview does not initialize or write a data profile");
            File.WriteAllText(profile.StatePath,"{\"version\":1,\"checked\":[],\"lastCompleted\":\"\"}");
            var legacy=new Profile(directory);
            Check(legacy.State.LastCompleted is null,"Legacy blank completion date loads as no completion");
            File.WriteAllText(profile.StatePath,"{broken progress");
            bool stateRejected=false;try{_=new Profile(directory);}catch(JsonException){stateRejected=true;}
            Check(stateRejected && File.ReadAllText(profile.StatePath)=="{broken progress","Malformed progress is rejected and preserved");
            File.WriteAllText(profile.StatePath,"{\"version\":999,\"checked\":[]}");
            bool stateVersionRejected=false;try{_=new Profile(directory);}catch(InvalidDataException){stateVersionRejected=true;}
            Check(stateVersionRejected,"Unsupported progress version is rejected");
            Check(Profile.InstanceName(directory)==Profile.InstanceName(directory.ToUpperInvariant()+Path.DirectorySeparatorChar) && Profile.InstanceName(directory)!=Profile.InstanceName(directory+"-other"),"Single-instance identity matches legacy canonical profile paths");
            Check(Profile.ResolveTarget("knownfolder:Documents")==Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Documents follows the Windows known folder");
            bool unknownRejected=false;try{Profile.ResolveTarget("knownfolder:Unknown");}catch(InvalidDataException){unknownRejected=true;}Check(unknownRejected,"Unknown folder tokens are rejected");
            Check(MainWindow.SearchTopics("progress").Length>0 && MainWindow.SearchTopics("unmatched-9751").Length==0,"Help search handles matching and missing topics");
            var routingDir=Path.Combine(root,"routing profile "+Guid.NewGuid().ToString("N"));
            string routingName=Profile.InstanceName(routingDir);
            using(var routeMutex=new Mutex(true,routingName))
            using(var manualSignal=new EventWaitHandle(false,EventResetMode.AutoReset,routingName+"-activate"))
            using(var scheduledSignal=new EventWaitHandle(false,EventResetMode.AutoReset,routingName+"-reminder"))
            {
                try
                {
                    foreach(bool scheduled in new[]{true,false})
                    {
                        using var duplicate=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) {Arguments="--data-directory "+WindowsReminderScheduler.Quote(routingDir)+(scheduled?" --scheduled":""),UseShellExecute=false,CreateNoWindow=true})!;
                        Check(duplicate.WaitForExit(5000)&&duplicate.ExitCode==0,"Duplicate launch exits cleanly (scheduled="+scheduled+")");
                        Check(scheduledSignal.WaitOne(0)==scheduled&&manualSignal.WaitOne(0)!=scheduled,"Duplicate launch distinguishes scheduled reminders from manual activation");
                    }
                }
                finally{routeMutex.ReleaseMutex();}
            }
            var attentionDir=Path.Combine(root,"attention-"+Guid.NewGuid().ToString("N"));
            using(var attentionWindow=new MainWindow(new Profile(attentionDir),Options.Parse(["--data-directory",attentionDir,"--self-test",root])))attentionWindow.VerifyReminderAttention(Check);
            var checkedDir=Path.Combine(root,"checked-times-"+Guid.NewGuid().ToString("N"));
            var checkedProfile=new Profile(checkedDir);checkedProfile.Save(["documents"],null);
            using(var checkedWindow=new MainWindow(checkedProfile,Options.Parse(["--data-directory",checkedDir,"--self-test",root])))checkedWindow.VerifyCheckedTimestamps(Check);
            var invalidStampPath=checkedProfile.StatePath;
            File.WriteAllText(invalidStampPath,"{\"version\":1,\"checked\":[\"documents\"],\"checkedAt\":{\"documents\":\"invalid\"}}");
            string invalidStampText=File.ReadAllText(invalidStampPath);bool invalidStampRejected=false;
            try{_=new Profile(checkedDir);}catch(InvalidDataException){invalidStampRejected=true;}
            Check(invalidStampRejected&&File.ReadAllText(invalidStampPath)==invalidStampText,"Malformed checked timestamps are rejected without rewriting progress");
            var options=Options.Parse(["--data-directory",Path.Combine(root,"ui-"+Guid.NewGuid().ToString("N")),"--preview",Path.Combine(root,"verification.png")]);
            using(var window=new MainWindow(new Profile(options.DataDirectory,preview:true),options))
            {
                Check(window.ItemCount==5&&!window.CanComplete,"Executable starts with five unchecked generic tasks");
                window.RenderPreview(options.PreviewPath!);
                Check(!Directory.Exists(options.DataDirectory),"UI preview preserves isolated profile storage");
            }

            string editorDir=Path.Combine(root,"editor-"+Guid.NewGuid().ToString("N"));
            var editorProfile=new Profile(editorDir);
            var settingsDraft=editorProfile.CopySettings();
            settingsDraft.Extra=new(){["futureOption"]=JsonSerializer.SerializeToElement("keep")};
            settingsDraft.Items[0].Extra=new(){["futureItemOption"]=JsonSerializer.SerializeToElement(42)};
            editorProfile.SaveSettings(settingsDraft);
            byte[] beforeEdit=File.ReadAllBytes(editorProfile.SettingsPath);
            var isolatedDraft=editorProfile.CopySettings();isolatedDraft.Items[0].Title="Draft only";
            Check(editorProfile.Settings.Items[0].Title!="Draft only"&&beforeEdit.SequenceEqual(File.ReadAllBytes(editorProfile.SettingsPath)),"Settings drafts do not mutate live configuration");
            using(var window=new MainWindow(editorProfile,Options.Parse(["--data-directory",editorDir,"--self-test",root])))window.VerifySettingsWindows(Check);
            var edited=new Profile(editorDir);
            Check(edited.Settings.Extra?["futureOption"].GetString()=="keep"&&edited.Settings.Items[0].Extra?["futureItemOption"].GetInt32()==42,"Editor preserves unknown configuration fields");
            string stateText=File.ReadAllText(edited.StatePath);
            var monthly=edited.CopySettings();monthly.Schedule!.Mode="Monthly";monthly.Schedule.DayOfMonth=31;edited.SaveSettings(monthly);
            Check(new Profile(editorDir).Settings.Schedule?.DayOfMonth==31&&File.ReadAllText(edited.StatePath)==stateText,"Monthly preferences round-trip without changing progress");
            var memory=new MemoryReminderScheduler();
            var scheduleDraft=edited.CopySettings();scheduleDraft.Schedule!.Enabled=true;
            var savedConfig=File.ReadAllBytes(edited.SettingsPath);
            bool rejectedSave=false;
            using(var lockedConfig=new FileStream(edited.SettingsPath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {try{ReminderSettings.Save(edited,scheduleDraft,memory);}catch(Exception error) when(error is IOException or UnauthorizedAccessException){rejectedSave=true;}}
            Check(rejectedSave&&memory.Read().Xml is null&&savedConfig.SequenceEqual(File.ReadAllBytes(edited.SettingsPath)),"Failed config replacement rolls back task registration and preserves settings");
            ReminderSettings.Save(edited,scheduleDraft,memory);var activeTask=memory.Read();
            scheduleDraft.Schedule.Enabled=false;
            using(var lockedConfig=new FileStream(edited.SettingsPath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {try{ReminderSettings.Save(edited,scheduleDraft,memory);}catch(Exception error) when(error is IOException or UnauthorizedAccessException){}}
            Check(memory.Read()==activeTask,"Failed disable save restores the prior reminder task");
            ReminderSettings.Save(edited,scheduleDraft,memory);
            Check(memory.Read().Xml is null&&!edited.Settings.Schedule!.Enabled,"Disable removes only the selected profile's reminder");
            var invalidSchedule=edited.CopySettings();invalidSchedule.Schedule!.IntervalDays=0;
            bool scheduleRejected=false;try{edited.SaveSettings(invalidSchedule);}catch(InvalidDataException){scheduleRejected=true;}
            Check(scheduleRejected,"Invalid schedule intervals are rejected");
            File.AppendAllText(edited.SettingsPath," ");bool externalRejected=false;
            try{edited.SaveSettings(edited.CopySettings());}catch(IOException){externalRejected=true;}
            Check(externalRejected&&File.ReadAllText(edited.SettingsPath).EndsWith(" "),"External configuration changes are preserved and rejected on save");
            string scheduleBytes=File.ReadAllText(edited.SettingsPath);
            edited.Save([],"2026-10-17T12:00:00-05:00");
            Check(File.ReadAllText(edited.SettingsPath)==scheduleBytes,"Completing backups does not reset or skip saved schedule preferences");
            File.WriteAllLines(report,results);return 0;
        }
        catch(Exception error){results.Add("FAIL: "+error);File.WriteAllLines(report,results);return 1;}
    }
}

internal sealed partial class MainWindow
{
    internal void VerifyCheckedTimestamps(Action<bool,string> check)
    {
        Show();Application.DoEvents();
        Label StampLabel(CheckBox box)=>box.Parent!.Controls.OfType<Label>().Single(x=>x.Name=="CheckedAt");
        var legacy=boxes.Single(x=>(string)x.Tag! =="documents");
        check(legacy.Checked&&StampLabel(legacy).Text=="Checked date not recorded"&&!checkedTimes.ContainsKey("documents"),"Legacy checked items retain an explicitly unknown date");
        var first=boxes[0];string id=(string)first.Tag!;first.Checked=true;
        string firstStamp=checkedTimes[id];
        check(DateTimeOffset.TryParse(firstStamp,out _)&&StampLabel(first).Visible&&StampLabel(first).Text.StartsWith("Checked "),"Checking an item shows its recorded date and time");
        ReloadChecklist();first=boxes.Single(x=>(string)x.Tag! ==id);
        check(first.Checked&&checkedTimes[id]==firstStamp,"Checklist reload preserves unsaved check timestamps by stable ID");
        checkedTimes[id]="2025-01-01T09:00:00-06:00";RefreshCheckedLabel(first);
        first.Checked=false;
        check(!checkedTimes.ContainsKey(id)&&!StampLabel(first).Visible,"Unchecking clears and hides the prior checked timestamp");
        first.Checked=true;string latest=checkedTimes[id];
        check(latest!="2025-01-01T09:00:00-06:00","Rechecking records a fresh confirmation time");
        profile.Save([id,"documents"],null,checkedTimes);
        var reopened=new Profile(profile.DirectoryPath);
        check(reopened.State.CheckedAt[id]==latest&&!reopened.State.CheckedAt.ContainsKey("documents"),"Saved timestamps round-trip without fabricating legacy dates");
        string stateText=File.ReadAllText(profile.StatePath);
        var attempted=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {[id]="2026-01-01T00:00:00Z"};bool failed=false;
        using(var locked=new FileStream(profile.StatePath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {try{profile.Save([id],null,attempted);}catch(Exception error) when(error is IOException or UnauthorizedAccessException){failed=true;}}
        check(failed&&profile.State.CheckedAt[id]==latest&&File.ReadAllText(profile.StatePath)==stateText,"Failed save retains the previous timestamps and progress bytes");
        Close();
        reopened=new Profile(profile.DirectoryPath);
        check(reopened.State.CheckedAt[id]==latest,"Closing and reopening preserves the displayed checked time");
        reopened.Save([],"2026-10-17T12:00:00-05:00");
        check(new Profile(profile.DirectoryPath).State.CheckedAt.Count==0,"Completing the routine clears individual checked timestamps");
    }
}
