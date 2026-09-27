window.pokehexDownload = function (filename, base64) {
  const raw = atob(base64);
  const bytes = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
  const url = URL.createObjectURL(new Blob([bytes]));
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
};

window.pokehexBindDrop = function (dotNetRef, elementId) {
  const el = document.getElementById(elementId);
  if (!el || el.dataset.pokehexDrop === "1") return;
  el.dataset.pokehexDrop = "1";
  el.addEventListener("dragover", function (event) { event.preventDefault(); });
  el.addEventListener("drop", async function (event) {
    event.preventDefault();
    const file = event.dataTransfer && event.dataTransfer.files && event.dataTransfer.files[0];
    if (!file) return;
    const bytes = new Uint8Array(await file.arrayBuffer());
    let binary = "";
    const chunk = 0x8000;
    for (let i = 0; i < bytes.length; i += chunk) {
      binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
    }
    await dotNetRef.invokeMethodAsync("LoadDroppedFile", file.name, btoa(binary));
  });
};
