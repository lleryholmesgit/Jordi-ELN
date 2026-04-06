(() => {
    const startButton = document.querySelector("[data-start-qr-scan]");
    const video = document.querySelector("[data-qr-video]");
    const payloadField = document.querySelector("[data-qr-payload]");

    if (!startButton || !video || !payloadField) return;

    startButton.addEventListener("click", async () => {
        if (!navigator.mediaDevices?.getUserMedia) {
            window.alert("Camera access is not available in this browser.");
            return;
        }

        const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" } });
        video.srcObject = stream;
        await video.play();

        if (!("BarcodeDetector" in window)) {
            window.alert("Barcode Detector is not available. Paste the QR payload manually or use the mobile app API.");
            return;
        }

        const detector = new BarcodeDetector({ formats: ["qr_code"] });
        const scan = async () => {
            if (video.readyState < 2) {
                requestAnimationFrame(scan);
                return;
            }

            try {
                const barcodes = await detector.detect(video);
                if (barcodes.length > 0) {
                    payloadField.value = barcodes[0].rawValue;
                    stream.getTracks().forEach((track) => track.stop());
                    return;
                }
            } catch {
            }

            requestAnimationFrame(scan);
        };

        scan();
    });
})();