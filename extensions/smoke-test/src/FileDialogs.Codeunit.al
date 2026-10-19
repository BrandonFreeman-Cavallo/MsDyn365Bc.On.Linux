// Regression guard for issue #122, items 2 and 4: what a test run's file dialogs answer.
//
// A Windows container runs tests through the web server's client-services session. Its
// file dialog provider hands a download to the caller as a link and refuses an upload.
// So DownloadFromStream returns true and leaves the file name as it was, and
// DataFileInformation (an upload of a .navdata file) fails with
// NotSupportedFileDialogProvider. Before the fix the websocket runner answered with a null
// result (a NullReferenceException in AL) and the hub said false to both, without an error.
//
// Both runners are covered: tools/TestRunner's FileActionDialog answers as that client
// does, and Patch #36 makes the hub's TestRunnerClientCallback do the same.
codeunit 70012 "BC Linux File Dialog Tests"
{
    Subtype = Test;
    TestPermissions = Disabled;

    var
        LibraryAssert: Codeunit "Library Assert";

    [Test]
    procedure DownloadFromStreamReturnsTrueAndKeepsTheFileName()
    var
        Blob: Record "BC Linux Windows Text Blob" temporary;
        OutS: OutStream;
        InS: InStream;
        ToFile: Text;
    begin
        Blob.Data.CreateOutStream(OutS);
        OutS.WriteText('download content');
        Blob.Data.CreateInStream(InS);
        ToFile := 'bclinux-download.txt';
        LibraryAssert.IsTrue(DownloadFromStream(InS, 'Title', '', '', ToFile), 'DownloadFromStream');
        LibraryAssert.AreEqual('bclinux-download.txt', ToFile, 'ToFile');
    end;

    [Test]
    procedure DataFileInformationIsNotSupported()
    var
        Company: Record Company;
        FileName: Text;
        Description: Text;
        TenantId: Text;
        HasApplication: Boolean;
        HasApplicationData: Boolean;
        HasGlobalData: Boolean;
        ExportDate: DateTime;
    begin
        FileName := TemporaryPath() + 'bclinux-missing.navdata';
        asserterror Database.DataFileInformation(false, FileName, Description, HasApplication, HasApplicationData, HasGlobalData, TenantId, ExportDate, Company);
        LibraryAssert.ExpectedError('The operation cannot complete because it uses a feature that is not supported (AL File function.)');
        LibraryAssert.AreEqual('NotSupportedFileDialogProvider:NotSupportedFileDialogProvider', GetLastErrorCode(), 'error code');
    end;

    // Issue #125: ExportData writes its export on a background thread into a pipe that blocks
    // until the client reads the download. Accepting the download without reading it made
    // every ExportData wait for the pipe's 60-second timeout and end in an uncatchable
    // TimeoutException. The runner now reads the stream, as Microsoft's client does.
    [Test]
    procedure ExportDataDoesNotWaitForATimeout()
    var
        Started: DateTime;
    begin
        Started := CurrentDateTime();
        if TryExportGlobalData(TemporaryPath() + 'bclinux-export.navdata') then;
        LibraryAssert.IsTrue(CurrentDateTime() - Started < 30000, 'ExportData took 30 s or more');
    end;

    [TryFunction]
    local procedure TryExportGlobalData(FileName: Text)
    begin
        Database.ExportData(false, FileName, 'bc-linux smoke test', false, false, true);
    end;
}
