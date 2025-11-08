document.addEventListener("DOMContentLoaded", () => {
    const status = document.getElementById("status");
    const btnPushback = document.getElementById("btnPushback");
    const btnChecklist = document.getElementById("btnChecklist");
    const btnConfig = document.getElementById("btnConfig");

    btnPushback.addEventListener("click", () => {
        status.textContent = "Iniciando pushback...";
        console.log("FlyGround → Pushback iniciado");

        // Aquí se conectará con la API de MSFS en el futuro
        // Ejemplo (cuando se integre con SimConnect o Coherent.call):
        // Coherent.call("FLYGROUND.PUSHBACK_START");
    });

    btnChecklist.addEventListener("click", () => {
        status.textContent = "Abriendo checklist...";
        console.log("FlyGround → Checklist abierto");
    });

    btnConfig.addEventListener("click", () => {
        status.textContent = "Abriendo configuración...";
        console.log("FlyGround → Configuración abierta");
    });
});
