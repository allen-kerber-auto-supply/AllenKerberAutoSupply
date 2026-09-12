import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UserAccount } from '../shared/models';

@Component({
  selector: 'app-user-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './user-admin.component.html',
  styleUrl: './user-admin.component.css'
})
export class UserAdminComponent {
  @Input() users: UserAccount[] = [];
  @Input() loadingUsers = false;
  @Input() usersError = '';
  @Input() newUser = { displayName: '', email: '', temporaryPassword: '', roles: [] as string[] };
  @Input() roleOptions: string[] = [];
  @Input() adminMessage = '';
  @Input() adminError = false;
  @Input() resettingUser: UserAccount | null = null;
  @Input() resetPasswordValue = '';
  @Input() editingRolesUser: UserAccount | null = null;
  @Input() editingRoles: string[] = [];
  @Input() deletingUser: UserAccount | null = null;
  @Input() isSelf: (user: UserAccount) => boolean = () => false;
  @Input() hasRole: (role: string) => boolean = () => false;
  @Input() hasEditingRole: (role: string) => boolean = () => false;

  @Output() backRequested = new EventEmitter<void>();
  @Output() createRequested = new EventEmitter<void>();
  @Output() retryRequested = new EventEmitter<void>();
  @Output() roleToggled = new EventEmitter<{ role: string; selected: boolean }>();
  @Output() generateNewUserPassword = new EventEmitter<void>();
  @Output() generateResetPassword = new EventEmitter<void>();
  @Output() resetStarted = new EventEmitter<UserAccount>();
  @Output() editRolesStarted = new EventEmitter<UserAccount>();
  @Output() deleteStarted = new EventEmitter<UserAccount>();
  @Output() resetRequested = new EventEmitter<void>();
  @Output() rolesSaveRequested = new EventEmitter<void>();
  @Output() deleteRequested = new EventEmitter<void>();
  @Output() resetClosed = new EventEmitter<void>();
  @Output() rolesClosed = new EventEmitter<void>();
  @Output() deleteClosed = new EventEmitter<void>();
  @Output() editingRoleToggled = new EventEmitter<{ role: string; selected: boolean }>();
}
