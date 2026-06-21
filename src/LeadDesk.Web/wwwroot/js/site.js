(() => {
    function initialiseMultiPicker(select) {
        if (select.dataset.enhanced === 'true') return;
        select.dataset.enhanced = 'true';

        const picker = document.createElement('div');
        picker.className = 'multi-picker';
        picker.innerHTML = `
            <button type="button" class="multi-picker-trigger" aria-haspopup="listbox" aria-expanded="false">
                <span class="multi-picker-value"></span>
                <i class="bi bi-chevron-down multi-picker-chevron" aria-hidden="true"></i>
            </button>
            <div class="multi-picker-menu">
                <div class="multi-picker-search-wrap">
                    <i class="bi bi-search" aria-hidden="true"></i>
                    <input type="search" class="multi-picker-search" autocomplete="off" />
                </div>
                <div class="multi-picker-actions">
                    <button type="button" class="multi-picker-action" data-picker-action="all">Select all</button>
                    <button type="button" class="multi-picker-action" data-picker-action="clear">Clear</button>
                </div>
                <div class="multi-picker-options" role="listbox" aria-multiselectable="true"></div>
                <div class="multi-picker-empty">No matching options</div>
            </div>`;

        select.insertAdjacentElement('afterend', picker);
        const trigger = picker.querySelector('.multi-picker-trigger');
        const value = picker.querySelector('.multi-picker-value');
        const search = picker.querySelector('.multi-picker-search');
        const optionsContainer = picker.querySelector('.multi-picker-options');
        const empty = picker.querySelector('.multi-picker-empty');
        search.placeholder = select.dataset.searchPlaceholder || 'Search...';

        Array.from(select.options).forEach((option, index) => {
            const label = document.createElement('label');
            label.className = 'multi-picker-option';
            label.dataset.search = option.text.toLocaleLowerCase();
            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.value = option.value;
            checkbox.setAttribute('aria-label', option.text);
            const optionText = document.createElement('span');
            optionText.textContent = option.text;
            label.append(checkbox, optionText);
            checkbox.checked = option.selected;
            checkbox.dataset.optionIndex = index;
            label.classList.toggle('selected', option.selected);
            checkbox.addEventListener('change', () => {
                option.selected = checkbox.checked;
                label.classList.toggle('selected', checkbox.checked);
                select.dispatchEvent(new Event('change', { bubbles: true }));
                renderValue();
            });
            optionsContainer.appendChild(label);
        });

        function renderValue() {
            const selected = Array.from(select.selectedOptions);
            value.replaceChildren();
            if (!selected.length) {
                const placeholder = document.createElement('span');
                placeholder.className = 'multi-picker-placeholder';
                placeholder.textContent = select.dataset.placeholder || 'Select options';
                value.appendChild(placeholder);
                return;
            }
            selected.slice(0, 2).forEach(option => {
                const chip = document.createElement('span');
                chip.className = 'multi-picker-chip';
                chip.textContent = option.text;
                value.appendChild(chip);
            });
            if (selected.length > 2) {
                const count = document.createElement('span');
                count.className = 'multi-picker-chip multi-picker-count';
                count.textContent = `+${selected.length - 2} more`;
                value.appendChild(count);
            }
        }

        function setOpen(open) {
            document.querySelectorAll('.multi-picker.open').forEach(other => {
                if (other !== picker) {
                    other.classList.remove('open');
                    other.querySelector('.multi-picker-trigger').setAttribute('aria-expanded', 'false');
                }
            });
            picker.classList.toggle('open', open);
            trigger.setAttribute('aria-expanded', String(open));
            if (open) {
                search.value = '';
                filterOptions('');
                requestAnimationFrame(() => search.focus());
            }
        }

        function filterOptions(term) {
            let visible = 0;
            optionsContainer.querySelectorAll('.multi-picker-option').forEach(label => {
                const matches = label.dataset.search.includes(term.toLocaleLowerCase());
                label.hidden = !matches;
                if (matches) visible++;
            });
            empty.style.display = visible ? 'none' : 'block';
        }

        trigger.addEventListener('click', () => setOpen(!picker.classList.contains('open')));
        search.addEventListener('input', () => filterOptions(search.value.trim()));
        picker.querySelector('[data-picker-action="all"]').addEventListener('click', () => {
            optionsContainer.querySelectorAll('.multi-picker-option:not([hidden]) input').forEach(input => {
                if (!input.checked) { input.checked = true; input.dispatchEvent(new Event('change')); }
            });
        });
        picker.querySelector('[data-picker-action="clear"]').addEventListener('click', () => {
            optionsContainer.querySelectorAll('input:checked').forEach(input => {
                input.checked = false; input.dispatchEvent(new Event('change'));
            });
        });
        picker.addEventListener('keydown', event => { if (event.key === 'Escape') { setOpen(false); trigger.focus(); } });
        renderValue();
    }

    document.addEventListener('click', event => {
        document.querySelectorAll('.multi-picker.open').forEach(picker => {
            if (!picker.contains(event.target)) {
                picker.classList.remove('open');
                picker.querySelector('.multi-picker-trigger').setAttribute('aria-expanded', 'false');
            }
        });
    });

    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('select[data-multi-select]').forEach(initialiseMultiPicker);
    });
})();
