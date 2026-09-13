import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SalesCall } from '../shared/models';

@Component({
  selector: 'app-scheduled-calls',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './scheduled-calls.component.html',
  styleUrl: './scheduled-calls.component.css'
})
export class ScheduledCallsComponent {
  @Input() calls: SalesCall[] = [];
  @Input() loading = false;
  @Input() isAdmin = false;
  @Input() dateFrom = '';
  @Input() dateTo = '';
  @Input() accountFilter = '';
  @Input() accountOptions: string[] = [];

  @Output() newCallRequested = new EventEmitter<void>();
  @Output() callSelected = new EventEmitter<SalesCall>();
  @Output() completeRequested = new EventEmitter<SalesCall>();
  @Output() deleteRequested = new EventEmitter<SalesCall>();
  @Output() filtersChanged = new EventEmitter<void>();
  @Output() accountFilterChange = new EventEmitter<string>();
  @Output() dateRangeChange = new EventEmitter<{ dateFrom: string; dateTo: string }>();

  get filteredCalls(): SalesCall[] {
    const query = this.accountFilter.trim().toLowerCase();
    return this.calls.filter(call => {
      const callDate = (call.callDate || call.createdDate || '').slice(0, 10);
      const matchesDate = (!this.dateFrom || callDate >= this.dateFrom) && (!this.dateTo || callDate <= this.dateTo);
      const matchesAccount = !query || (call.accountName || '').toLowerCase().includes(query);
      return matchesDate && matchesAccount;
    });
  }

  onAccountFilterChanged(value: string) {
    this.accountFilterChange.emit(value);
    this.filtersChanged.emit();
  }

  onDateRangeChanged() {
    this.dateRangeChange.emit({ dateFrom: this.dateFrom, dateTo: this.dateTo });
    this.filtersChanged.emit();
  }

  onCardKeydown(event: KeyboardEvent, call: SalesCall) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.callSelected.emit(call);
    }
  }
}
