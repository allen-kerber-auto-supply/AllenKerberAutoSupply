import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccountSummary, SalesCall } from '../shared/models';
import { formatSalesCallDate } from './sales-date-format';

export type HistoryViewMode = 'records' | 'summary';

@Component({
  selector: 'app-sales-history',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './sales-history.component.html',
  styleUrl: './sales-history.component.css'
})
export class SalesHistoryComponent implements OnChanges {
  @Input() isAdmin = false;
  @Input() viewMode: HistoryViewMode = 'records';
  @Input() loading = false;
  @Input() loadingMore = false;
  @Input() totalCount = 0;
  @Input() dateFrom = '';
  @Input() dateTo = '';
  @Input() accountFilter = '';
  @Input() accountOptions: string[] = [];
  @Input() calls: SalesCall[] = [];
  @Input() summaries: AccountSummary[] = [];
  @Input() selectedSummaryAccount = '';
  @Input() selectedSummaryCalls: SalesCall[] = [];
  @Input() isProspect: (call: SalesCall) => boolean = () => false;
  @Input() repDisplayName: (name?: string, email?: string) => string = (name, email) => name || email || '—';

  @Output() viewModeChange = new EventEmitter<HistoryViewMode>();
  @Output() filtersChanged = new EventEmitter<void>();
  @Output() dateRangeChange = new EventEmitter<{ dateFrom: string; dateTo: string }>();
  @Output() accountFilterChange = new EventEmitter<string>();
  @Output() exportRequested = new EventEmitter<void>();
  @Output() printRequested = new EventEmitter<void>();
  @Output() callSelected = new EventEmitter<SalesCall>();
  @Output() editRequested = new EventEmitter<SalesCall>();
  @Output() deleteRequested = new EventEmitter<SalesCall>();
  @Output() summarySelected = new EventEmitter<AccountSummary>();
  @Output() summaryClosed = new EventEmitter<void>();
  @Output() loadMoreRequested = new EventEmitter<void>();

  visibleCalls: SalesCall[] = [];

  ngOnChanges(changes: SimpleChanges) {
    if (changes['calls']) {
      this.visibleCalls = this.calls;
    }
  }

  formatCallDate(dateString?: string): string {
    return formatSalesCallDate(dateString);
  }

  setViewMode(viewMode: HistoryViewMode) {
    this.viewModeChange.emit(viewMode);
    this.filtersChanged.emit();
  }

  onAccountFilterChanged(value: string) {
    this.accountFilterChange.emit(value);
    this.filtersChanged.emit();
  }

  onDateRangeChanged() {
    this.dateRangeChange.emit({ dateFrom: this.dateFrom, dateTo: this.dateTo });
    this.filtersChanged.emit();
  }

  onCallListScroll(event: Event) {
    const element = event.target as HTMLElement;
    if (element.scrollTop + element.clientHeight < element.scrollHeight - 160) return;
    if (this.loadingMore || this.calls.length >= this.totalCount) return;

    this.loadMoreRequested.emit();
  }

  onCardKeydown(event: KeyboardEvent, call: SalesCall) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.callSelected.emit(call);
    }
  }

  onSummaryKeydown(event: KeyboardEvent, summary: AccountSummary) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.summarySelected.emit(summary);
    }
  }
}
