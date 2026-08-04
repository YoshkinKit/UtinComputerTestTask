using System.Collections.Generic;
using UnityEngine;

namespace Game.Utils
{
    /// <summary>
    /// Простой дженерик-пул компонентов на GameObject. Не имеет внешних зависимостей:
    /// сам инстанцирует префаб при нехватке свободных объектов и переключает
    /// активность GameObject при получении/возврате.
    /// </summary>
    /// <typeparam name="T">Тип компонента, размещённого на корне пуллируемого префаба.</typeparam>
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _inactive = new Stack<T>();

        /// <summary>
        /// Создаёт пул и заранее прогревает его заданным количеством инстансов.
        /// </summary>
        /// <param name="prefab">Префаб (или любой шаблонный инстанс) с компонентом T.</param>
        /// <param name="parent">Родитель для создаваемых инстансов (может быть null).</param>
        /// <param name="prewarmCount">Сколько инстансов создать заранее.</param>
        public ObjectPool(T prefab, Transform parent, int prewarmCount)
        {
            _prefab = prefab;
            _parent = parent;

            for (int i = 0; i < prewarmCount; i++)
            {
                T instance = CreateInstance();
                instance.gameObject.SetActive(false);
                _inactive.Push(instance);
            }
        }

        /// <summary>
        /// Возвращает свободный инстанс (активированный), создавая новый при пустом пуле.
        /// </summary>
        public T Get()
        {
            T instance = _inactive.Count > 0 ? _inactive.Pop() : CreateInstance();
            instance.gameObject.SetActive(true);
            return instance;
        }

        /// <summary>
        /// Деактивирует инстанс и возвращает его в пул для повторного использования.
        /// </summary>
        public void Release(T instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.gameObject.SetActive(false);
            _inactive.Push(instance);
        }

        private T CreateInstance()
        {
            return Object.Instantiate(_prefab, _parent);
        }
    }
}
