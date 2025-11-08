document.addEventListener("DOMContentLoaded", () => {
    const status = document.getElementById("status");
    const btnPushback = document.getElementById("btnPushback");

    btnPushback.addEventListener("click", async () => {
        status.textContent = "Solicitando pushback...";
        try {
            const payload = { heading: 90, speed: 1 }; // ejemplo: heading en grados
            const res = await fetch("http://localhost:5000/pushback", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
            if (!res.ok) throw new Error("No ok: " + res.status);
            const json = await res.json();
            if (json.ok) {
                status.textContent = "Pushback solicitado correctamente.";
            } else {
                status.textContent = "Pushback: respuesta inesperada";
            }
        } catch (err) {
            console.error(err);
            status.textContent = "Error al solicitar pushback. ¿Arrancaste FlyGroundBridge?";
        }
    });
});
