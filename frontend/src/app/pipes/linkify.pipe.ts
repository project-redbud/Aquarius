import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Pipe({ name: 'linkify', standalone: true })
export class LinkifyPipe implements PipeTransform {
  constructor(private sanitizer: DomSanitizer) {}

  transform(value: string | null | undefined): SafeHtml {
    if (!value) return '';

    // 1. Escape HTML
    const escaped = value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');

    // 2. Replace URLs, bare domains, and b#N references
    // Order matters: match full URLs first, then bare domains, then b#N
    const linked = escaped.replace(
      /(https?:\/\/[^\s<]+)|((?:[\w-]+\.)+[a-z]{2,}(?:\/[^\s<]*)?(?:\?[^\s<]*)?)|(b#(\d+))/gi,
      (match, url, domain, _d2, bottleId) => {
        if (url) {
          return `<a href="${url}" target="_blank" rel="noopener">${url}</a>`;
        }
        if (domain) {
          // Bare domain: auto-prefix with https://
          return `<a href="https://${domain}" target="_blank" rel="noopener">${domain}</a>`;
        }
        if (bottleId) {
          return `<a href="/bottle/${bottleId}" target="_blank">b#${bottleId}</a>`;
        }
        return match;
      }
    );

    return this.sanitizer.bypassSecurityTrustHtml(linked);
  }
}
