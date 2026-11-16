using Firebase.Database;
using System;
using System.Collections.Generic;
using System.Text;
using Firebase.Database.Query;
using Duty.Models;
namespace Duty.Services
{
  /// <summary>
  /// Модель подключения к удаленной бд. Без возможности авторизации.
  /// </summary>
    public class FirebaseDataService
    {
        private readonly FirebaseClient _firebase;
        private const string RootNode = "duty_data";

        public FirebaseDataService(FirebaseClient firebase)
        {
            _firebase = firebase;
        }

        // 1. Чтение всего состояния для всех пользователей
        public async Task<DutyData> LoadDataAsync()
        {
            var data = await _firebase
                .Child(RootNode)
                .OnceSingleAsync<DutyData>();

            return data ?? new DutyData();
        }

        // 2. Полная перезапись состояния (вызывается только в админе)
        public async Task SaveDataAsync(DutyData dataToSave)
        {
            dataToSave.UpdatedAt = DateTimeOffset.Now;

            await _firebase
                .Child(RootNode)
                .PutAsync(dataToSave);
        }

    }
}
