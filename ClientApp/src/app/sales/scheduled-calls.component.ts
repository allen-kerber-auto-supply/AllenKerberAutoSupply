import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SalesCall } from '../shared/models';
import { formatSalesCallDate } from './sales-date-format';

@Component({
  selector: 'app-scheduled-calls',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './scheduled-calls.component.html',
  styleUrl: './scheduled-calls.component.css'
})
export class ScheduledCallsComponent implements OnChanges {
  private readonly batchSize = 30;
  private visibleCount = this.batchSize;

  @Input() calls: SalesCall[] = [];
  @Input() loading = false;
  @Input() isAdmin = false;
  @Input() accountFilter = '';
  @Input() accountOptions: string[] = [];

  @Output() newCallRequested = new EventEmitter<void>();
  @Output() callSelected = new EventEmitter<SalesCall>();
  @Output() completeRequested = new EventEmitter<SalesCall>();
  @Output() deleteRequested = new EventEmitter<SalesCall>();
  @Output() filtersChanged = new EventEmitter<void>();
  @Output() accountFilterChange = new EventEmitter<string>();

  ngOnChanges(changes: SimpleChanges) {
    if (changes['calls'] || changes['accountFilter']) {
      this.visibleCount = this.batchSize;
    }
  }

  formatCallDate(dateString?: string): string {
    return formatSalesCallDate(dateString);
  }

  get filteredCalls(): SalesCall[] {
    const query = this.accountFilter.trim().toLowerCase();
    return this.calls.filter(call => {
      const matchesAccount = !query || (call.accountName || '').toLowerCase().includes(query);
      return matchesAccount;
    });
  }

  get visibleCalls(): SalesCall[] {
    return this.filteredCalls.slice(0, this.visibleCount);
  }

  onCallListScroll() {
    if (window.scrollY + window.innerHeight < document.documentElement.scrollHeight - 160) return;
    if (this.visibleCount >= this.filteredCalls.length) return;

    this.visibleCount = Math.min(this.visibleCount + this.batchSize, this.filteredCalls.length);
  }

  onAccountFilterChanged(value: string) {
    this.accountFilterChange.emit(value);
    this.filtersChanged.emit();
  }

  onCardKeydown(event: KeyboardEvent, call: SalesCall) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.callSelected.emit(call);
    }
  }
}
