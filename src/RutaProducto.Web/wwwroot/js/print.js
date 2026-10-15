// Opens the browser's print dialog (imported by PrintButton through IJSRuntime). The PDF of a document is
// "Save as PDF" in that dialog: the page has a print style sheet that leaves only the document on the paper.
export function printPage() {
    window.print();
}
