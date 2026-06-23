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

    // 2. Replace URLs and b#N references
    const linked = escaped.replace(
      /(https?:\/\/[^\s]+)|(b#(\d+))/g,
      (match, url, _bRef, bottleId) => {
        if (url) {
          return `<a href="${url}" target="_blank" rel="noopener">${url}</a>`;
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
