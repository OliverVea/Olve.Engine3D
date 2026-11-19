export function save(name, bytes) {
    const url = URL.createObjectURL(new Blob([bytes.slice()], { type: 'application/octet-stream' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

export function open(accept) {
    return new Promise((resolve, reject) => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = accept;
        input.style.display = 'none';
        const finish = settle => {
            input.remove();
            settle();
        };
        input.addEventListener('cancel', () => finish(() => resolve(null)));
        input.addEventListener('change', () => {
            const file = input.files?.[0];
            if (!file) {
                finish(() => resolve(null));
                return;
            }
            file.arrayBuffer().then(
                buffer => finish(() => resolve(new Uint8Array(buffer))),
                error => finish(() => reject(error)));
        });
        document.body.appendChild(input);
        input.click();
    });
}

export function length(bytes) {
    return bytes.length;
}

export function copy(source, target) {
    target.set(source);
}
