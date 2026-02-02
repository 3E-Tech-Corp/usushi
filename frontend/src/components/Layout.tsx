import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

const customerNav = [
  { path: '/', label: '🍣 Dashboard' },
  { path: '/upload', label: '📸 Upload Receipt' },
  { path: '/meals', label: '🍱 My Meals' },
  { path: '/rewards', label: '🎁 Rewards' },
  { path: '/notifications', label: '🔔 Notifications' },
];

const adminNav = [
  { path: '/admin', label: '📊 Dashboard' },
  { path: '/admin/users', label: '👥 Users' },
  { path: '/admin/rewards', label: '🎁 Rewards' },
  { path: '/admin/sms', label: '📱 SMS Broadcast' },
];

export default function Layout() {
  const { user, logout, isAdmin } = useAuth();

  const formatPhone = (phone: string) => {
    if (phone.length === 10) {
      return `(${phone.slice(0, 3)}) ${phone.slice(3, 6)}-${phone.slice(6)}`;
    }
    return phone;
  };

  return (
    <div className="min-h-screen bg-gray-900 flex">
      {/* Sidebar */}
      <aside className="w-64 bg-gray-800 border-r border-gray-700 flex flex-col flex-shrink-0">
        <div className="p-6 border-b border-gray-700">
          <div className="flex items-center">
            <span className="text-3xl mr-2">🍣</span>
            <div>
              <h1 className="text-xl font-bold text-white">USushi</h1>
              <p className="text-xs text-gray-400">Loyalty Rewards</p>
            </div>
          </div>
        </div>

        <nav className="flex-1 p-4 space-y-1 overflow-y-auto">
          {/* Customer Nav */}
          <p className="text-xs text-gray-500 uppercase tracking-wider px-4 mb-2">Menu</p>
          {customerNav.map((item) => (
            <NavLink
              key={item.path}
              to={item.path}
              end={item.path === '/'}
              className={({ isActive: active }) =>
                `block px-4 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                  active
                    ? 'bg-sushi-600 text-white'
                    : 'text-gray-300 hover:bg-gray-700 hover:text-white'
                }`
              }
            >
              {item.label}
            </NavLink>
          ))}

          {/* Admin Nav */}
          {isAdmin && (
            <>
              <div className="pt-4 pb-2">
                <p className="text-xs text-gray-500 uppercase tracking-wider px-4">Admin</p>
              </div>
              {adminNav.map((item) => (
                <NavLink
                  key={item.path}
                  to={item.path}
                  end={item.path === '/admin'}
                  className={({ isActive: active }) =>
                    `block px-4 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                      active
                        ? 'bg-sushi-600 text-white'
                        : 'text-gray-300 hover:bg-gray-700 hover:text-white'
                    }`
                  }
                >
                  {item.label}
                </NavLink>
              ))}
            </>
          )}
        </nav>

        <div className="p-4 border-t border-gray-700">
          <div className="flex items-center justify-between">
            <div className="min-w-0">
              <p className="text-sm font-medium text-white truncate">
                {user?.displayName || (user?.phone ? formatPhone(user.phone) : 'User')}
              </p>
              <p className="text-xs text-gray-400">
                {user?.role}{user?.displayName && user?.phone ? ` · ${formatPhone(user.phone)}` : ''}
              </p>
            </div>
            <button
              onClick={logout}
              className="text-sm text-gray-400 hover:text-white transition-colors ml-2 flex-shrink-0"
            >
              Logout
            </button>
          </div>
        </div>
      </aside>

      {/* Main content */}
      <main className="flex-1 p-8 overflow-auto">
        <Outlet />
      </main>
    </div>
  );
}
