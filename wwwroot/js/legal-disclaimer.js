/**
 * Cenzas Analytics - Legal Disclaimer Component
 * Namespace: window.CenzasAnalytics.UI.LegalDisclaimer
 */

window.CenzasAnalytics = window.CenzasAnalytics || {};
window.CenzasAnalytics.UI = window.CenzasAnalytics.UI || {};

window.CenzasAnalytics.UI.LegalDisclaimer = {
    modalId: 'cenzas-legal-modal',
    accepted: false,

    init: function () {
        console.log("[LegalDisclaimer] Initializing (Per-page mandatory check)...");
        this.render();
        this.show();
    },

    render: function () {
        if (document.getElementById(this.modalId)) return;

        const overlay = document.createElement('div');
        overlay.id = this.modalId;
        overlay.className = 'modal-overlay';

        overlay.innerHTML = `
            <div class="modal-content" style="max-width: 600px;">
                <div class="modal-header">
                    <h3>Svarbi informacija dėl pateikiamų duomenų</h3>
                </div>
                <div class="modal-body">
                    <div style="font-family: var(--font-ui); color: #475569; line-height: 1.6;">
                        <p style="margin-bottom: 16px;">Visi sistemoje pateikiami duomenys yra informacinio pobūdžio. „Cenzas“ neatsako už turtinius ar neturtinius nuostolius, patirtus priimant sprendimus remiantis šia statistika.</p>
                        <p style="margin-bottom: 16px;">Duomenys yra renkami iš viešų NT portalų ir gali tuėti paklaidų dėl pirminių šaltinių netikslumų, dublikatų ar pasenusios informacijos.</p>
                        <p>Tęsdami darbą patvirtinate, kad suprantate ir sutinkate su šiomis sąlygomis.</p>
                    </div>
                </div>
                <div class="modal-footer" style="gap: 12px; display: flex;">
                    <button id="btn-disclaimer-decline" class="btn btn--secondary" style="flex: 1;">Supratau, nesutinku!</button>
                    <button id="btn-disclaimer-accept" class="btn btn--primary" style="flex: 1;">Supratau, sutinku!</button>
                </div>
            </div>
        `;

        document.body.appendChild(overlay);

        document.getElementById('btn-disclaimer-accept').addEventListener('click', () => this.handleAccept());
        document.getElementById('btn-disclaimer-decline').addEventListener('click', () => this.handleDecline());
    },

    show: function () {
        const modal = document.getElementById(this.modalId);
        if (modal) {
            modal.classList.add('modal--open');
            document.body.style.overflow = 'hidden';
        }
    },

    hide: function () {
        const modal = document.getElementById(this.modalId);
        if (modal) {
            modal.classList.remove('modal--open');
            document.body.style.overflow = '';
        }
    },

    handleAccept: function () {
        console.log("[LegalDisclaimer] Consent accepted for this page session.");
        this.accepted = true;
        this.hide();

        // Dispatch custom event to notify other modules
        window.dispatchEvent(new CustomEvent('cenzas:consent_given'));
    },

    handleDecline: function () {
        console.log("[LegalDisclaimer] Consent declined. Redirecting...");
        window.location.href = 'index.html';
    },

    isAccepted: function () {
        return this.accepted;
    }
};
