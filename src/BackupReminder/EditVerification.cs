namespace BackupReminder;
internal sealed partial class MainWindow
{
    void VerifyNestedUnsavedPrompts(Action<bool,string> check)
    {
        foreach(bool schedule in new[]{false,true})
        foreach(string action in new[]{"Keep editing","Discard changes","Save changes","X"})
        {
            using var editor=schedule?CreateScheduleSettings():CreateChecklistEditor();
            string before=File.ReadAllText(profile.SettingsPath);
            if(schedule)
            {
                var day=Descendants<ThemeNumberInput>(editor).Single(x=>x.Name=="ScheduleDay");day.Value=day.Value==31?30:day.Value+1;
            }
            else Descendants<TextBox>(editor).Single(x=>x.Name=="EntryName").Text+=" nested "+action;
            Exception? failure=null;bool sawPrompt=false;
            using var closeTimer=new System.Windows.Forms.Timer {Interval=60};
            using var promptTimer=new System.Windows.Forms.Timer {Interval=40};
            promptTimer.Tick+=(_,_)=>
            {
                var prompt=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="Unsaved changes"&&x.Visible);
                if(prompt is null)return;promptTimer.Stop();sawPrompt=true;
                try
                {
                    check(editor.Modal&&editor.Owner==this&&prompt.Modal&&prompt.Owner==editor,"Real modal ownership is main > "+editor.Text+" > unsaved prompt ("+action+")");
                    check(!WindowStyle.IsWindowEnabled(editor.Handle)&&!WindowStyle.IsWindowEnabled(Handle),"Nested confirmation disables both owner windows");
                    if(action=="X")prompt.Close();else Descendants<Button>(prompt).Single(x=>x.Text==action).PerformClick();
                }
                catch(Exception error){failure=error;prompt.DialogResult=DialogResult.Cancel;prompt.Close();}
            };
            closeTimer.Tick+=(_,_)=>
            {
                closeTimer.Stop();
                try
                {
                    promptTimer.Start();editor.Close();
                    if(action is "Keep editing" or "X")
                    {
                        check(editor.Visible&&WindowStyle.IsWindowEnabled(editor.Handle),"Real "+action+" returns to the edited window");
                        ShowRealUnsavedPromptForTest=false;UnsavedDecisionForTest=_=>DialogResult.No;editor.Close();
                    }
                }
                catch(Exception error){failure=error;editor.Dispose();}
                finally{promptTimer.Stop();}
            };
            ShowRealUnsavedPromptForTest=true;closeTimer.Start();
            try{ShowOwnedDialog(editor);}finally{ShowRealUnsavedPromptForTest=false;}
            if(failure is not null)throw new InvalidDataException("Real nested dialog failed: "+editor.Text+" / "+action,failure);
            check(sawPrompt,"Real nested confirmation opened without an ownership exception");
            bool changed=File.ReadAllText(profile.SettingsPath)!=before;
            check(changed==(action=="Save changes"),"Real "+action+" applies the expected persistence behavior for "+editor.Text);
        }
    }
    internal void VerifyEditSessions(Action<bool,string> check)
    {
        Show();Application.DoEvents();
        string original=File.ReadAllText(profile.SettingsPath);
        using(var editor=CreateChecklistEditor())
        {
            editor.Show(this);Application.DoEvents();
            var name=Descendants<TextBox>(editor).Single(x=>x.Name=="EntryName");
            var save=Descendants<Button>(editor).Single(x=>x.Name=="SaveChecklist");string title=name.Text;
            check(save.Text=="Saved"&&!save.Enabled,"Unchanged checklist opens with a disabled Saved button");
            name.Text=title+" changed";check(save.Text=="Save changes"&&save.Enabled,"Editing a checklist enables Save changes");
            name.Text=title;check(save.Text=="Saved"&&!save.Enabled,"Reverting checklist edits returns to Saved");
            name.Text=title+" discarded";int prompts=0;
            UnsavedDecisionForTest=owner=>{check(owner==editor,"Confirmation belongs to the edited window");prompts++;return DialogResult.Cancel;};
            editor.Close();check(editor.Visible&&prompts==1,"Window X with unsaved changes supports Keep editing");
            UnsavedDecisionForTest=_=>DialogResult.No;
            Descendants<Button>(editor).Single(x=>x.Name=="CancelEditor").PerformClick();
            check(!editor.Visible&&File.ReadAllText(profile.SettingsPath)==original,"Cancel/Escape discard closes without saving checklist edits");
        }
        using(var editor=CreateChecklistEditor())
        {
            editor.Show(this);Application.DoEvents();
            Descendants<TextBox>(editor).Single(x=>x.Name=="EntryName").Text="Saved through confirmation";
            UnsavedDecisionForTest=_=>DialogResult.Yes;editor.Close();
            check(!editor.Visible&&profile.Settings.Items[0].Title=="Saved through confirmation","Save changes in the close confirmation saves and closes the checklist");
        }
        using(var editor=CreateChecklistEditor())
        {
            editor.Show(this);Application.DoEvents();
            var name=Descendants<TextBox>(editor).Single(x=>x.Name=="EntryName");name.Text="Saved without closing";
            var save=Descendants<Button>(editor).Single(x=>x.Name=="SaveChecklist");save.PerformClick();
            check(editor.Visible&&save.Text=="Saved"&&!save.Enabled&&profile.Settings.Items[0].Title==name.Text,"Successful checklist save stays open and disables Saved");
            name.Text+=" again";check(save.Enabled&&save.Text=="Save changes","A later checklist edit re-enables saving");
            UnsavedDecisionForTest=_=>DialogResult.No;editor.Close();
        }
        using(var schedule=CreateScheduleSettings())
        {
            schedule.Show(this);Application.DoEvents();
            var save=Descendants<Button>(schedule).Single(x=>x.Name=="SaveSchedule");
            var day=Descendants<ThemeNumberInput>(schedule).Single(x=>x.Name=="ScheduleDay");
            check(save.Text=="Saved"&&!save.Enabled,"Unchanged reminder opens with disabled Saved");
            day.Value=day.Value==31?30:day.Value+1;
            check(save.Text=="Save schedule"&&save.Enabled,"Changing reminder timing enables Save schedule");
            save.PerformClick();check(save.Text=="Saved"&&!save.Enabled&&schedule.Visible,"Saving the reminder visibly confirms Saved without closing");
            Descendants<Button>(schedule).Single(x=>x.Name=="SoundOptions").PerformClick();
            Descendants<Button>(schedule).Single(x=>x.Name=="PreviewReminderSound").PerformClick();
            check(save.Text=="Saved"&&!save.Enabled,"Sound Preview and expanding options do not create unsaved changes");
            var sound=Descendants<CheckBox>(schedule).Single(x=>x.Name=="PlayReminderSound");sound.Checked=!sound.Checked;
            check(save.Enabled&&save.Text=="Save schedule","Notification edits re-enable Save schedule");
            UnsavedDecisionForTest=_=>DialogResult.Cancel;
            Descendants<Button>(schedule).Single(x=>x.Name=="CancelSchedule").PerformClick();
            check(schedule.Visible,"Reminder Close supports Keep editing for unsaved changes");
            UnsavedDecisionForTest=_=>DialogResult.Yes;schedule.Close();
            check(!schedule.Visible&&profile.Settings.PlayReminderSound==sound.Checked,"Reminder confirmation saves notification changes before closing");
        }
        using(var clean=CreateScheduleSettings())
        {
            clean.Show(this);Application.DoEvents();
            UnsavedDecisionForTest=_=>throw new InvalidDataException("Unchanged dialogs must not prompt.");
            clean.Close();check(!clean.Visible,"Already saved windows close without a confirmation");
        }
        using(var prompt=CreateUnsavedPrompt())
        {
            var buttons=Descendants<Button>(prompt).ToArray();
            check(buttons.Length==3&&buttons.Select(x=>x.Text).ToHashSet().SetEquals(new[]{"Save changes","Discard changes","Keep editing"})&&prompt.CancelButton==buttons.Single(x=>x.Text=="Keep editing"),"Unsaved prompt offers three explicit choices with Escape mapped to Keep editing");
        }
        // Exercise a failing save without opening an unattended error message.
        using(var form=new Form())
        {
            var input=new TextBox();var save=new Button();form.Controls.AddRange([input,save]);form.Show();
            var session=new EditSession(form,save,"Save changes",()=>input.Text,()=>false,()=>true,()=>DialogResult.Yes);
            input.Text="pending";save.PerformClick();
            check(session.Dirty&&save.Enabled&&save.Text=="Save changes","A failed save never shows Saved or clears dirty state");
            form.Close();check(form.Visible&&session.Dirty,"A failed Save changes from the close prompt keeps the window open");
        }
        VerifyNestedUnsavedPrompts(check);
        UnsavedDecisionForTest=null;Close();
    }
}
