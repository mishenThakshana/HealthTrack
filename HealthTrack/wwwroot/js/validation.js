$.validator.addMethod('mustbetrue', (_, element) => element.checked);
$.validator.unobtrusive.adapters.addBool('mustbetrue');
