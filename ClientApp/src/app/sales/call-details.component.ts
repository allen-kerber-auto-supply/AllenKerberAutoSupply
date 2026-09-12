import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { SalesCall } from '../shared/models';

@Component({
  selector: 'app-call-details',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './call-details.component.html',
  styleUrl: './call-details.component.css'
})
export class CallDetailsComponent {
  @Input() call: SalesCall | null = null;
  @Input() isProspect = false;
  @Input() converting = false;

  @Output() closed = new EventEmitter<void>();
  @Output() convertRequested = new EventEmitter<void>();
  @Output() editRequested = new EventEmitter<SalesCall>();

  formatCallDuration(duration?: number): string {
    if (duration == null || duration < 0) return '—';

    const hours = Math.floor(duration / 60);
    const minutes = duration % 60;
    return `${hours}:${minutes.toString().padStart(2, '0')}`;
  }

  formatCallDate(dateString?: string): string {
    if (!dateString) return '—';

    // Parse ISO format date/time string preserving wall-clock time
    // Format: YYYY-MM-DDTHH:mm:ss or YYYY-MM-DDTHH:mm:ssZ
    const match = /^(\d{4})-(\d{2})-(\d{2})(?:[T\s](\d{2}):(\d{2}))?/.exec(dateString);
    if (!match) return '—';

    const year = parseInt(match[1], 10);
    const month = parseInt(match[2], 10) - 1; // Month is 0-indexed
    const day = parseInt(match[3], 10);
    const hours = match[4] ? parseInt(match[4], 10) : 0;
    const minutes = match[5] ? parseInt(match[5], 10) : 0;

    // Create date object representing the stored wall-clock time
    const date = new Date(year, month, day, hours, minutes, 0);

    // Format as M/d/yyyy h:mm a
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    const monthName = months[date.getMonth()];
    const dayNum = date.getDate();
    const yearNum = date.getFullYear();
    const hour12 = date.getHours() % 12 || 12;
    const ampm = date.getHours() < 12 ? 'AM' : 'PM';
    const minStr = date.getMinutes().toString().padStart(2, '0');

    return `${date.getMonth() + 1}/${dayNum}/${yearNum} ${hour12}:${minStr} ${ampm}`;
  }
}
