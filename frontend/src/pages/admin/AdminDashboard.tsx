import { useState, useEffect } from 'react';
import { Users, UtensilsCrossed, Gift, TrendingUp } from 'lucide-react';
import api from '../../services/api';

interface MealDto {
  id: number;
  userId: number;
  extractedTotal: number | null;
  extractedRestaurant: string | null;
  manualTotal: number | null;
  status: string;
  createdAt: string;
}

interface AdminStats {
  totalUsers: number;
  mealsToday: number;
  activeRewards: number;
  pendingRewards: number;
  recentMeals: MealDto[];
}

export default function AdminDashboard() {
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<AdminStats>('/admin/dashboard')
      .then(setStats)
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString('en-US', {
      month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit'
    });
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <div className="text-gray-400">Loading admin dashboard...</div>
      </div>
    );
  }

  return (
    <div>
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-white">Admin Dashboard</h1>
        <p className="text-gray-400 mt-1">USushi Loyalty Program Overview</p>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
          <div className="flex items-center justify-between mb-2">
            <Users className="text-blue-400" size={24} />
          </div>
          <p className="text-3xl font-bold text-white">{stats?.totalUsers ?? 0}</p>
          <p className="text-gray-400 text-sm">Total Users</p>
        </div>

        <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
          <div className="flex items-center justify-between mb-2">
            <UtensilsCrossed className="text-sushi-400" size={24} />
          </div>
          <p className="text-3xl font-bold text-white">{stats?.mealsToday ?? 0}</p>
          <p className="text-gray-400 text-sm">Meals Today</p>
        </div>

        <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
          <div className="flex items-center justify-between mb-2">
            <Gift className="text-green-400" size={24} />
          </div>
          <p className="text-3xl font-bold text-white">{stats?.activeRewards ?? 0}</p>
          <p className="text-gray-400 text-sm">Active Rewards</p>
        </div>

        <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
          <div className="flex items-center justify-between mb-2">
            <TrendingUp className="text-purple-400" size={24} />
          </div>
          <p className="text-3xl font-bold text-white">{stats?.pendingRewards ?? 0}</p>
          <p className="text-gray-400 text-sm">Pending Rewards</p>
        </div>
      </div>

      {/* Recent Activity */}
      <div className="bg-gray-800 rounded-xl border border-gray-700">
        <div className="p-5 border-b border-gray-700">
          <h3 className="text-white font-semibold">Recent Activity</h3>
        </div>
        {(!stats?.recentMeals || stats.recentMeals.length === 0) ? (
          <div className="p-8 text-center text-gray-400">No recent activity</div>
        ) : (
          <div className="divide-y divide-gray-700">
            {stats.recentMeals.map(meal => (
              <div key={meal.id} className="px-5 py-4 flex items-center justify-between">
                <div>
                  <p className="text-white font-medium">
                    User #{meal.userId} — {meal.extractedRestaurant || 'Restaurant'}
                  </p>
                  <p className="text-gray-400 text-sm">{formatDate(meal.createdAt)}</p>
                </div>
                <div className="text-right">
                  <p className="text-white font-semibold">
                    {(meal.manualTotal ?? meal.extractedTotal) 
                      ? `$${(meal.manualTotal ?? meal.extractedTotal)!.toFixed(2)}` 
                      : 'N/A'}
                  </p>
                  <span className={`text-xs px-2 py-0.5 rounded-full ${
                    meal.status === 'Verified' ? 'bg-green-900/50 text-green-400' : 'bg-yellow-900/50 text-yellow-400'
                  }`}>
                    {meal.status}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
