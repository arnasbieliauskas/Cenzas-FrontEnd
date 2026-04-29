document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('loan-form');
    if (!form) return;

    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        const submitBtn = form.querySelector('button[type="submit"]');
        const originalBtnText = submitBtn.innerText;
        submitBtn.innerText = "Siunčiama...";
        submitBtn.disabled = true;

        const formData = new FormData(form);
        
        // Collect data using PascalCase keys matching the form name attributes
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
                alert("Paraiška sėkmingai išsiųsta");
                form.reset();
            } else {
                const errorData = await response.json().catch(() => ({}));
                alert(errorData.message || "Įvyko klaida siunčiant paraišką. Bandykite dar kartą.");
            }
        } catch (error) {
            console.error('Error:', error);
            alert("Nepavyko susisiekti su serveriu. Patikrinkite interneto ryšį.");
        } finally {
            submitBtn.innerText = originalBtnText;
            submitBtn.disabled = false;
        }
    });
});
