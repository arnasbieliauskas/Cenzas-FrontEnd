window.initLoanForm = function () {
    const form = document.getElementById('loan-form');
    if (!form) return;

    // Rule #14: Clear error state on input event
    form.querySelectorAll('.form-control').forEach(input => {
        input.addEventListener('input', () => {
            input.classList.remove('is-invalid');
            input.parentElement.classList.remove('has-error');
        });
    });

    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        // Rule #14: Validate required fields on submit
        let isValid = true;
        form.querySelectorAll('[required]').forEach(input => {
            if (!input.value.trim()) {
                input.classList.add('is-invalid');
                input.parentElement.classList.add('has-error');
                isValid = false;
            }
        });

        if (!isValid) return;

        const submitBtn = form.querySelector('button[type="submit"]') || form.querySelector('.form-submit');
        const originalBtnText = submitBtn.innerText;
        submitBtn.innerText = "Siunčiama...";
        submitBtn.disabled = true;

        const formData = new FormData(form);

        // Collect data using PascalCase keys matching the form name attributes (Rule #13)
        const data = {
            Amount: formData.get('Amount'),
            LoanTerm: formData.get('LoanTerm'),
            PropertyType: formData.get('PropertyType'),
            PropertyAddress: formData.get('PropertyAddress'),
            Phone: formData.get('Phone'),
            Email: formData.get('Email'),
            Name: formData.get('Name'),
            Other: formData.get('Other')
        };

        try {
            const response = await fetch('/api/Contact/submit', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(data)
            });

            if (response.ok) {
                const successModal = document.getElementById('success-modal');
                if (successModal) {
                    successModal.classList.add('active');
                    form.reset();

                    // Auto-close after 5 seconds
                    setTimeout(() => {
                        successModal.classList.remove('active');
                    }, 5000);
                } else {
                    alert("Paraiška sėkmingai išsiųsta. Artimiausiu metu susisieksime su jumis.");
                    form.reset();
                }
            } else {
                const errorData = await response.json().catch(() => ({}));
                const msg = errorData.message || "Įvyko klaida siunčiant paraišką. Bandykite dar kartą.";
                showError(msg);
            }
        } catch (error) {
            console.error('Error:', error);
            showError("Nepavyko susisiekti su serveriu. Patikrinkite interneto ryšį.");
        } finally {
            submitBtn.innerText = originalBtnText;
            submitBtn.disabled = false;
        }
    });
};

const showError = (message) => {
    const errorModal = document.getElementById('error-modal');
    const errorMsg = document.getElementById('error-modal-msg');
    if (errorModal && errorMsg) {
        errorMsg.innerText = message;
        errorModal.classList.add('active');
    } else {
        alert(message);
    }
};

// Global Modal Close Handler (Event Delegation)
document.addEventListener('click', (e) => {
    if (e.target.id === 'close-modal-btn' || e.target.closest('#close-modal-btn')) {
        const modal = document.getElementById('success-modal');
        if (modal) modal.classList.remove('active');
    }
    if (e.target.id === 'close-error-modal-btn' || e.target.closest('#close-error-modal-btn')) {
        const modal = document.getElementById('error-modal');
        if (modal) modal.classList.remove('active');
    }
});

// Auto-init if form is already in DOM (e.g. static pages)
document.addEventListener('DOMContentLoaded', () => {
    window.initLoanForm();
});
