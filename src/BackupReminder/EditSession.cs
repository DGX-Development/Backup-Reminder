namespace BackupReminder;

// One saved baseline drives both the button and every user-initiated close path.
internal sealed class EditSession
{
    readonly Form dialog;
    readonly Button button;
    readonly Func<string> snapshot;
    readonly Func<bool> save;
    readonly Func<bool> valid;
    readonly Func<DialogResult> confirm;
    readonly string saveCaption;
    readonly Action? refreshStyle;
    string baseline;
    bool saving,confirming,requireApply;
    internal bool Dirty=>snapshot()!=baseline;
    internal EditSession(Form dialog,Button button,string saveCaption,Func<string> snapshot,Func<bool> save,Func<bool> valid,Func<DialogResult> confirm,bool requireApply=false,Action? refreshStyle=null)
    {
        this.dialog=dialog;this.button=button;this.saveCaption=saveCaption;this.snapshot=snapshot;this.save=save;this.valid=valid;this.confirm=confirm;this.requireApply=requireApply;this.refreshStyle=refreshStyle;
        baseline=snapshot();button.Click+=(_,_)=>Save();
        dialog.FormClosing+=OnClosing;
        Watch(dialog);Refresh();
    }
    void Watch(Control parent)
    {
        foreach(Control child in parent.Controls)
        {
            if(child is TextBoxBase text)text.TextChanged+=(_,_)=>Refresh();
            if(child is CheckBox check)check.CheckedChanged+=(_,_)=>Refresh();
            if(child is EditorPicker picker)picker.SelectedIndexChanged+=(_,_)=>Refresh();
            if(child is ThemeNumberInput number)number.ValueChanged+=(_,_)=>Refresh();
            if(child is ScheduleDateTimeInput date)date.ValueChanged+=(_,_)=>Refresh();
            Watch(child);
        }
    }
    internal void Refresh()
    {
        if(saving||dialog.IsDisposed)return;
        bool pending=Dirty||requireApply;
        button.Text=pending?saveCaption:"Saved";
        button.Enabled=pending&&valid();
        button.Cursor=button.Enabled?Cursors.Hand:Cursors.Default;
        refreshStyle?.Invoke();
        button.AccessibleDescription=pending?"Unsaved changes. "+saveCaption+" to keep them.":"All changes are saved.";
    }
    internal bool Save()
    {
        if(saving)return false;
        saving=true;
        try
        {
            if(!save())return false;
            baseline=snapshot();requireApply=false;return true;
        }
        finally{saving=false;Refresh();}
    }
    void OnClosing(object? sender,FormClosingEventArgs e)
    {
        // Do not interrupt Windows shutdown. Closing normally, Cancel and Escape all come here.
        if(e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing or CloseReason.ApplicationExitCall)return;
        if(!Dirty)return;
        if(confirming){e.Cancel=true;return;}
        confirming=true;
        try
        {
            var choice=confirm();
            e.Cancel=choice!=DialogResult.No&&(choice!=DialogResult.Yes||!Save());
        }
        finally{confirming=false;}
    }
}

internal sealed partial class MainWindow
{
    void StyleSaveButton(Button button)
    {
        button.BackColor=button.Enabled?Accent:ButtonColor;
        button.ForeColor=button.Enabled?(dark?Color.FromArgb(20,35,55):Color.White):Muted;
    }
    internal Func<Form,DialogResult>? UnsavedDecisionForTest;
    internal bool ShowRealUnsavedPromptForTest;
    DialogResult ConfirmUnsaved(Form editor)
    {
        if(options.TestDirectory is not null&&!ShowRealUnsavedPromptForTest)return UnsavedDecisionForTest?.Invoke(editor)??DialogResult.No;
        using var prompt=CreateUnsavedPrompt();
        // This is a child of the editor, not another child of the disabled main window.
        prompt.Owner=null;
        return prompt.ShowDialog(editor);
    }
    Form CreateUnsavedPrompt()
    {
        var (prompt,content,footer)=StyledPage("Unsaved changes","Save your changes before closing?",new Size(590,310));
        content.Controls.Add(new Label {Text="Save to keep your changes, discard them, or keep editing.",Dock=DockStyle.Fill,Padding=new Padding(2,12,2,0)});
        var save=PageButton(footer,"Save changes",142,true);save.Name="SaveUnsaved";save.DialogResult=DialogResult.Yes;
        var discard=PageButton(footer,"Discard changes",150);discard.Name="DiscardUnsaved";discard.DialogResult=DialogResult.No;
        var keep=PageButton(footer,"Keep editing",132);keep.Name="KeepEditing";keep.DialogResult=DialogResult.Cancel;
        prompt.AcceptButton=save;prompt.CancelButton=keep;FinishPage(prompt);return prompt;
    }
}
