# Cenzas Frontend Development Rules

## 1. Global Component Architecture
* **Header & Footer:** MUST remain as global components in `wwwroot/components/`. 
* **Placeholders:** Every HTML file must use `<div id="header-placeholder"></div>` and `<div id="footer-placeholder"></div>`.
* **NO Inline Headers/Footers:** Never move the HTML code from components back into individual pages.

## 2. Business Logic Guardrails (main.js)
* **Legal Disclaimer:** The logic in `main.js` for showing the mandatory legal modal before NT analytics is SACRED. Do not modify the `showLegalDisclaimer` or navigation blocking logic.
* **Component Loading:** Use the `loadComponent` function for fetching HTML parts. Do not replace it with alternative fetch methods.

## 3. Visual Identity & Styling (styles.css)
* **Color Palette:** Do not change `:root` variables (Primary Blue, Black, White).
* **Typography:** Montserrat is the official font. Always use `text-transform: uppercase` for navigation links.
* **Header Look:** Must match `image_3e12f7.png` exactly: Black logo (45px height), blue underline for active links, black phone button on the right.

## 4. Asset Management
* **Local Only:** All images must be stored in `assets/images/`. NO remote URLs or `screenshot_...` files allowed.
* **Naming:** Use clean names like `logo-cenzas.png` and `hero-home.jpg`.

## 5. Scope Enforcement
* **Surgical Changes:** Change ONLY the requested element. Do not refactor surrounding sections or re-format the entire file.
* **Audit First:** Check if a placeholder or link is already present before adding a duplicate.

## 6. Home Page Layout (index.html)
* **Hero Grid:** Two-column layout (Text left / Image right).
* **Headline:** H1 "Paskola su nekilnojamojo turto įkeitimu" with sub-headline "BE PAJAMŲ VERTINIMO" in `--color-primary`.
* **Image Style:** `assets/images/hero-home.jpg` must have `border-radius: 40px`.
* **Info Cards:** Three cards with `box-shadow` must slightly overlap the Hero section using negative top margin.

## 7. Contacts Page Layout (kontaktai.html)
* **Content:** Display Address, Phone, and Working Hours (I-IV 10:00-17:00, V 10:00-15:00).
* **Office Image:** `assets/images/office-klaipeda.jpg` must have `border-radius: 40px` for consistency.
* **Map:** Full-width Google Maps iframe must be present at the bottom of the content.

## 8. Service Landing Pages (e.g., greitos-paskolos.html)
* **Hero Structure:** Must use a split 2-column grid layout. 
    * **Left Column:** Persuasive copy with H1 and a Blue sub-headline.
    * **Right Column:** Lead Generation Form (`.hero__form`) inside a white card container.
* **Standard Sections:** Every service page must follow this sequence: 
    1. Hero with Form -> 2. Advantages (using `.info-blocks` grid) -> 3. FAQ Section (Accordion style).
* **Form Consistency:** All input fields and selects must use global `.form__group` and `.btn--primary` classes to ensure visual alignment with the site's identity.

## 9. FAQ / Accordion Sections (DUK)
* **HTML Tags:** Use native `<details>` and `<summary>` tags for all FAQ sections to ensure performance and accessibility.
* **Styling:** * The `<summary>` must have a custom blue arrow/icon (using `::marker` or a background image).
    * Active/Open states must be styled in `styles.css` using the `details[open]` selector.
* **Layout:** FAQ blocks should be centered within a `.container` with a maximum width (e.g., 800px) to maintain readability.

## 10. Advantages & Benefits Grid
* **Structure:** Use a 3-column grid layout for desktop and 1-column for mobile.
* **Component:** Each benefit must be wrapped in an `.advantage-card` or `.info-card`.
* **Visual Elements:** * **Icons:** Must use the primary blue color (`--color-primary`) and be centered above the title.
    * **Titles:** Use `<h3>` for benefit titles to maintain SEO and visual hierarchy.
    * **Background:** Use a subtle hover effect (e.g., `transform: translateY(-5px)`) to enhance interactivity.
* **Consistency:** The spacing (padding/margin) between the Hero section and the Advantages section must be consistent across all subpages.

## 11. Full Application Page (pildyti-paraiska.html)
* **Layout:** Use a centered, single-column container (`.form-container`) with a maximum width of 800px for optimal readability.
* **Mandatory Fields:** The form MUST always include these 8 fields:
    1. Paskolos suma (Amount)
    2. Paskolos terminas (Term)
    3. Įkeičiamas turtas (Asset type - Select)
    4. Turto adresas (Address)
    5. Telefono numeris (Phone)
    6. El. paštas (Email)
    7. Vardas, Pavardė (Full Name)
    8. Papildoma informacija (Additional Info - Textarea)
* **Visual Hierarchy:** Group fields logically and ensure the "Siųsti paraišką" button is prominent (`.btn--primary`, full-width on mobile).
* **Validation:** Form labels must be clearly visible above inputs, and all required fields must have the `required` attribute.