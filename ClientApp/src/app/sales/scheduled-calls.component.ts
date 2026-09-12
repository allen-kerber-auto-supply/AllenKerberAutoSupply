import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { SalesCall } from '../shared/models';

@Component({
  selector: 'app-scheduled-calls',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './scheduled-calls.component.html',
  styleUrl: './scheduled-calls.component.css'
})
export class ScheduledCallsComponent {
  @Input() calls: SalesCall[] = [];
  @Input() loading = false;
  @Input() isAdmin = false;

  @Output() newCallRequested = new EventEmitter<void>();
  @Output() callSelected = new EventEmitter<SalesCall>();
  @Output() completeRequested = new EventEmitter<SalesCall>();
  @Output() deleteRequested = new EventEmitter<SalesCall>();

  onCardKeydown(event: KeyboardEvent, call: SalesCall) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.callSelected.emit(call);
    }
  }
}
