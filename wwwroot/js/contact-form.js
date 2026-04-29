window.initLoanForm = function () {
    const form = document.getElementById('loan-form');
    if (!form) return;

    // Rule #14: Currency Mask for Amount field
    const amountInput = form.querySelector('input[name="Amount"]');
    if (amountInput) {
        amountInput.addEventListener('input', (e) => {
            // Allow only digits
            e.target.value = e.target.value.replace(/\D/g, '');
        });

        amountInput.addEventListener('blur', (e) => {
            if (e.target.value) {
                // Append " €" suffix and thousand separators on blur
                const val = e.target.value.replace(/\D/g, '');
                if (val) {
                    const formatted = Number(val).toLocaleString('lt-LT');
                    e.target.value = formatted + ' €';
                }
            }
        });

        amountInput.addEventListener('focus', (e) => {
            // Remove " €" and all spaces/separators for clean editing
            e.target.value = e.target.value.replace(' €', '').replace(/\s/g, '');
        });
    }

    // Rule #14: Phone field validation (Digits only, '+' allowed at start)
    const phoneInput = form.querySelector('input[name="Phone"]');
    if (phoneInput) {
        phoneInput.addEventListener('input', (e) => {
            let val = e.target.value;
            const startsWithPlus = val.startsWith('+');
            // Remove everything that isn't a digit
            val = val.replace(/\D/g, '');
            // Add back the plus if it was at the start
            e.target.value = (startsWithPlus ? '+' : '') + val;
        });
    }

    // Rule #14: Name field validation (Letters and spaces only, including LT characters)
    const nameInput = form.querySelector('input[name="Name"]');
    if (nameInput) {
        nameInput.addEventListener('input', (e) => {
            e.target.value = e.target.value.replace(/[^a-zA-ZąčęėįšųūžĄČĘĖĮŠŲŪŽ\s]/g, '');
        });
    }

    // Rule #14: Clear error state on input/change event
    form.querySelectorAll('.form-control').forEach(input => {
        ['input', 'change'].forEach(eventType => {
            input.addEventListener(eventType, () => {
                input.classList.remove('is-invalid');
                input.parentElement.classList.remove('has-error');
            });
        });
    });

    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        // Rule #14: Validate required fields on submit
        let isValid = true;
        form.querySelectorAll('[required]').forEach(input => {
            const errorMsg = input.parentElement.querySelector('.error-message');
            
            if (!input.value.trim()) {
                if (errorMsg) errorMsg.innerText = "Šis laukas privalomas";
                input.classList.add('is-invalid');
                input.parentElement.classList.add('has-error');
                isValid = false;
            } else if (input.type === 'email' || input.name === 'Email') {
                const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
                if (!emailRegex.test(input.value)) {
                    if (errorMsg) errorMsg.innerText = "Neteisingas el. pašto formatas";
                    input.classList.add('is-invalid');
                    input.parentElement.classList.add('has-error');
                    isValid = false;
                }
            }
        });

        if (!isValid) return;

        const submitBtn = form.querySelector('button[type="submit"]') || form.querySelector('.form-submit');
        const originalBtnText = submitBtn.innerText;
        submitBtn.innerText = "Siunčiama...";
        submitBtn.disabled = true;

        const formData = new FormData(form);

        // Rule #14: Sanitize Amount value (remove " €" and spaces) before sending
        const rawAmount = formData.get('Amount') || "";
        const sanitizedAmount = rawAmount.replace(' €', '').replace(/\s/g, '');

        // Collect data using PascalCase keys matching the form name attributes (Rule #13)
        const data = {
            Amount: sanitizedAmount,
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
