import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SalesCall } from '../shared/models';

@Component({
  selector: 'app-call-edit-modals',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './call-edit-modals.component.html',
  styleUrl: './call-edit-modals.component.css'
})
export class CallEditModalsComponent {
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

  updateEditingCall(field: 'contactName' | 'phone' | 'callDate' | 'followUpDate' | 'status' | 'comments', value: string | number) {
    if (this.editingCall) {
      this.editingCallChange.emit({ ...this.editingCall, [field]: value });
    }
  }
}
