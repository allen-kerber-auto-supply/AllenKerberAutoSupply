import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SalesCall } from '../shared/models';

@Component({
  selector: 'app-call-edit-modals',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './call-edit-modals.component.html',
  styleUrl: './call-edit-modals.component.css'
})
export class CallEditModalsComponent implements OnChanges {
  @Input() completingCall: SalesCall | null = null;
  @Input() completingComments = '';
  @Input() completingFollowUpDate = '';
  @Input() editingCall: SalesCall | null = null;

  @Output() completingCommentsChange = new EventEmitter<string>();
  @Output() completingFollowUpDateChange = new EventEmitter<string>();
  @Output() editingCallChange = new EventEmitter<SalesCall>();
  @Output() completeRequested = new EventEmitter<void>();
  @Output() editRequested = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();

  editCallDate = '';
  editCallTime = '00:00';
  durationHours = 0;
  durationMinutes = 0;
  private callDateTimeSuffix = '';

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['editingCall']) {
      this.initializeEditTimingState();
    }
  }

  updateEditingCall(field: 'contactName' | 'phone' | 'followUpDate' | 'status' | 'comments', value: string | number) {
    if (this.editingCall) {
      this.editingCallChange.emit({ ...this.editingCall, [field]: value });
    }
  }

  onCallDateChange(value: string): void {
    this.editCallDate = value || '';
    this.emitCallDateTime();
  }

  onCallTimeChange(value: string): void {
    this.editCallTime = value || '00:00';
    this.emitCallDateTime();
  }

  onDurationHoursChange(value: string | number): void {
    this.updateDuration(this.toWholeNumber(value), this.durationMinutes);
  }

  onDurationMinutesChange(value: string | number): void {
    this.updateDuration(this.durationHours, this.toWholeNumber(value));
  }

  private initializeEditTimingState(): void {
    if (!this.editingCall) return;
    const parsedCallDateTime = this.parseCallDateTime(this.editingCall.callDate);
    this.editCallDate = parsedCallDateTime?.date || this.toDateInputValue(new Date());
    this.editCallTime = parsedCallDateTime?.time || '00:00';
    this.callDateTimeSuffix = parsedCallDateTime?.suffix || '';

    const duration = Number.isFinite(this.editingCall.callDuration) && (this.editingCall.callDuration ?? 0) > 0
      ? Number(this.editingCall.callDuration)
      : 0;
    this.durationHours = Math.floor(duration / 60);
    this.durationMinutes = duration % 60;
  }

  private emitCallDateTime(): void {
    if (!this.editingCall || !this.editCallDate) return;
    const time = this.parseCallTime(this.editCallTime) || '00:00';
    this.editingCallChange.emit({
      ...this.editingCall,
      callDate: `${this.editCallDate}T${time}:00${this.callDateTimeSuffix}`
    });
  }

  private updateDuration(hours: number, minutes: number): void {
    if (!this.editingCall) return;
    const normalizedHours = Math.min(8, Math.max(0, hours));
    const normalizedMinutes = Math.min(59, Math.max(0, minutes));
    const totalMinutes = normalizedHours === 8 && normalizedMinutes > 0
      ? 480
      : normalizedHours * 60 + normalizedMinutes;
    this.durationHours = Math.floor(totalMinutes / 60);
    this.durationMinutes = totalMinutes % 60;
    this.editingCallChange.emit({
      ...this.editingCall,
      callDuration: totalMinutes
    });
  }

  private toWholeNumber(value: string | number): number {
    const numericValue = typeof value === 'number' ? value : Number(value);
    if (!Number.isFinite(numericValue)) return 0;
    return Math.trunc(numericValue);
  }

  private parseCallDateTime(value?: string): { date: string; time: string; suffix: string } | null {
    const match = /^(\d{4}-\d{2}-\d{2})(?:[T\s](\d{2}):(\d{2})(?::\d{2}(?:\.\d+)?)?([Zz]|[+-]\d{2}(?::?\d{2}){1,2})?)?/.exec(value || '');
    if (!match) return null;
    const date = match[1];
    const time = match[2] && match[3] ? `${match[2]}:${match[3]}` : '00:00';
    const suffix = match[4] || '';
    return { date, time, suffix };
  }

  private parseCallTime(value: string): string | null {
    return /^([01]\d|2[0-3]):([0-5]\d)$/.test(value || '') ? value : null;
  }

  private toDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
